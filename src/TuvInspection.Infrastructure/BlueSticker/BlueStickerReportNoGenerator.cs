using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>Generates BSR-YYYY-NNNN. NNNN is a per-year running count (zero-padded to 4).</summary>
public sealed class BlueStickerReportNoGenerator
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public BlueStickerReportNoGenerator(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"BSR-{year}-";

        // Use IgnoreQueryFilters so counters aren't biased by tenant scope.
        var countThisYear = await _db.BlueStickerReports
            .IgnoreQueryFilters()
            .CountAsync(r => r.ReportNo.StartsWith(prefix), ct);

        return $"{prefix}{(countThisYear + 1):D4}";
    }
}
