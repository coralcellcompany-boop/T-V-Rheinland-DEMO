using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.JobManagement;

public sealed class JobRequestNoGenerator
{
    private readonly AppDbContext _db; private readonly IClock _clock;
    public JobRequestNoGenerator(AppDbContext db, IClock clock) { _db = db; _clock = clock; }
    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"JR{year}-";
        var count = await _db.JobRequests.IgnoreQueryFilters()
            .CountAsync(r => r.RequestNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}

public sealed class JobOrderNoGenerator
{
    private readonly AppDbContext _db; private readonly IClock _clock;
    public JobOrderNoGenerator(AppDbContext db, IClock clock) { _db = db; _clock = clock; }
    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"JOD{year}-";
        var count = await _db.JobOrders.IgnoreQueryFilters()
            .CountAsync(j => j.JobOrderNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}

public sealed class DwrNoGenerator
{
    private readonly AppDbContext _db; private readonly IClock _clock;
    public DwrNoGenerator(AppDbContext db, IClock clock) { _db = db; _clock = clock; }
    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"DWR{year}-";
        var count = await _db.DailyWorkReports.IgnoreQueryFilters()
            .CountAsync(d => d.DwrNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D5}";
    }
}

public sealed class SurveyNoGenerator
{
    private readonly AppDbContext _db; private readonly IClock _clock;
    public SurveyNoGenerator(AppDbContext db, IClock clock) { _db = db; _clock = clock; }
    public async Task<string> Next(CancellationToken ct)
    {
        var year = _clock.UtcNow.Year;
        var prefix = $"SUR{year}-";
        var count = await _db.Surveys.IgnoreQueryFilters()
            .CountAsync(s => s.SurveyNo.StartsWith(prefix), ct);
        return $"{prefix}{count + 1:D4}";
    }
}
