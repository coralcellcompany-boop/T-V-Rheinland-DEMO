using Microsoft.EntityFrameworkCore;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Stickers;

/// <summary>
/// Produces sticker numbers in the format <c>TUVR######</c> (TUVR + 6 zero-padded digits).
/// Pulls the highest existing number across all stickers and increments — adequate for MVP
/// volumes; high-throughput production should switch to a SQL sequence.
/// </summary>
public sealed class StickerNumberGenerator
{
    private readonly AppDbContext _db;
    public StickerNumberGenerator(AppDbContext db) => _db = db;

    public async Task<string> Next(CancellationToken ct)
    {
        var highest = await _db.Stickers.AsNoTracking()
            .OrderByDescending(s => s.StickerNo)
            .Select(s => s.StickerNo)
            .FirstOrDefaultAsync(ct);

        var seed = ParseSeed(highest);
        return Format(seed + 1);
    }

    public async Task<IReadOnlyList<string>> NextBatch(int count, CancellationToken ct)
    {
        if (count <= 0) return Array.Empty<string>();
        var highest = await _db.Stickers.AsNoTracking()
            .OrderByDescending(s => s.StickerNo)
            .Select(s => s.StickerNo)
            .FirstOrDefaultAsync(ct);
        var seed = ParseSeed(highest);
        return Enumerable.Range(1, count).Select(i => Format(seed + i)).ToList();
    }

    private static int ParseSeed(string? highest)
    {
        if (string.IsNullOrEmpty(highest)) return 0;
        var digits = new string(highest.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out var n) ? n : 0;
    }

    private static string Format(int n) => $"TUVR{n:D6}";
}
