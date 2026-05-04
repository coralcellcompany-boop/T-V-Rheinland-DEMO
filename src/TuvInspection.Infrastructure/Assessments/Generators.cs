using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Assessments;

/// <summary>
/// Produces assessment numbers in the format <c>ASMYYYY-NNNN</c> per year.
/// </summary>
public sealed class AssessmentNoGenerator
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    public AssessmentNoGenerator(AppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"ASM{year}-";
        var count = await _db.Assessments.IgnoreQueryFilters()
            .CountAsync(a => a.AssessmentNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}

/// <summary>
/// Produces competency card numbers in the format <c>TUVR-YY-NNNNNN</c> per SRS §4.3.
/// </summary>
public sealed class CompetencyCardNoGenerator
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    public CompetencyCardNoGenerator(AppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async Task<string> Next(CancellationToken ct)
    {
        var yy = (_clock.UtcNow.Year % 100).ToString("D2");
        var prefix = $"TUVR-{yy}-";
        var count = await _db.CompetencyCards.IgnoreQueryFilters()
            .CountAsync(c => c.CardNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D6}";
    }
}
