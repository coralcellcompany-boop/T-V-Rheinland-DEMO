using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.BlueSticker;
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
        var persisted = await _db.BlueStickerReports
            .IgnoreQueryFilters()
            .CountAsync(r => r.ReportNo.StartsWith(prefix), ct);

        // The create handler adds several reports (one per Aramco equipment) in a
        // single SaveChanges. Persisted count alone would return the same value for
        // every call in that loop and collide on the unique ReportNo index — so also
        // count reports already Added to this context but not yet saved.
        var pending = _db.ChangeTracker.Entries<BlueStickerReport>()
            .Count(e => e.State == EntityState.Added
                        && e.Entity.ReportNo != null
                        && e.Entity.ReportNo.StartsWith(prefix, StringComparison.Ordinal));

        return $"{prefix}{(persisted + pending + 1):D4}";
    }
}
