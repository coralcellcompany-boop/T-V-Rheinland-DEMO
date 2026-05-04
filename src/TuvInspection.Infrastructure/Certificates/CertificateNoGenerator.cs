using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Certificates;

/// <summary>
/// Produces certificate numbers in the format <c>IS-NNNNNN-YY-NNNN</c> per SRS §App-B.
/// First segment is a global rolling counter, second segment is the 2-digit year, third
/// segment is a per-year sequence. Both counters are derived from existing rows.
/// Note: production should use a SQL sequence/identity for race-safety; this is good enough
/// for MVP volumes.
/// </summary>
public sealed class CertificateNoGenerator
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public CertificateNoGenerator(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var yy = (year % 100).ToString("D2");

        // Use IgnoreQueryFilters so counters aren't biased by tenant scope.
        var totalCount = await _db.Certificates.IgnoreQueryFilters().CountAsync(ct);
        var perYearCount = await _db.Certificates.IgnoreQueryFilters()
            .Where(c => c.CertificateNo.Contains($"-{yy}-"))
            .CountAsync(ct);

        var rolling = (totalCount + 1).ToString("D6");
        var perYear = (perYearCount + 1).ToString("D4");
        return $"IS-{rolling}-{yy}-{perYear}";
    }
}
