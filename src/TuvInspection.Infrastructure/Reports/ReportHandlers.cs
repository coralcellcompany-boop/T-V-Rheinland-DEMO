using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.Reports;
using TuvInspection.Contracts.Reports;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Reports;

public sealed class GetMonthlyStatsHandler : IQueryHandler<GetMonthlyStatsQuery, IReadOnlyList<MonthlyStatsRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public GetMonthlyStatsHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<IReadOnlyList<MonthlyStatsRowDto>> Handle(GetMonthlyStatsQuery q, CancellationToken ct)
    {
        var months = Math.Clamp(q.Months, 1, 24);
        var now = _clock.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));

        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();
        certs = certs.Where(c => c.CreatedAtUtc >= start);

        var grouped = await certs
            .GroupBy(c => new { c.CreatedAtUtc.Year, c.CreatedAtUtc.Month })
            .Select(g => new
            {
                g.Key.Year, g.Key.Month,
                Total = g.Count(),
                Approved = g.Count(c => c.State == CertificateState.Approved
                                     || c.State == CertificateState.ClientSent
                                     || c.State == CertificateState.ClientAccepted
                                     || c.State == CertificateState.Archived),
                Rejected = g.Count(c => c.State == CertificateState.Rejected
                                     || c.State == CertificateState.Voided),
                InProgress = g.Count(c => c.State == CertificateState.Draft
                                       || c.State == CertificateState.Submitted
                                       || c.State == CertificateState.UnderReview
                                       || c.State == CertificateState.AwaitingApproval),
            }).ToListAsync(ct);

        // Fill in missing months with zeroes for a clean chart.
        var rows = new List<MonthlyStatsRowDto>(months);
        for (int i = 0; i < months; i++)
        {
            var m = start.AddMonths(i);
            var match = grouped.FirstOrDefault(x => x.Year == m.Year && x.Month == m.Month);
            rows.Add(new MonthlyStatsRowDto(
                $"{m.Year:D4}-{m.Month:D2}",
                match?.Total ?? 0,
                match?.Approved ?? 0,
                match?.Rejected ?? 0,
                match?.InProgress ?? 0));
        }
        return rows;
    }
}

public sealed class GetInspectorProductivityHandler
    : IQueryHandler<GetInspectorProductivityQuery, IReadOnlyList<InspectorProductivityRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public GetInspectorProductivityHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<IReadOnlyList<InspectorProductivityRowDto>> Handle(
        GetInspectorProductivityQuery q, CancellationToken ct)
    {
        var days = Math.Clamp(q.Days, 1, 365);
        var since = _clock.UtcNow.AddDays(-days);

        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();
        certs = certs.Where(c => c.CreatedAtUtc >= since);

        var byUser = await certs
            .Where(c => c.CreatedById != null)
            .GroupBy(c => c.CreatedById!)
            .Select(g => new
            {
                UserId = g.Key,
                Created = g.Count(),
                Approved = g.Count(c => c.State >= CertificateState.Approved
                                     && c.State != CertificateState.Rejected
                                     && c.State != CertificateState.Voided),
            }).ToListAsync(ct);

        var dwrByUser = await _db.DailyWorkReports
            .IgnoreQueryFilters().AsNoTracking()
            .Where(d => d.Date >= DateOnly.FromDateTime(since))
            .GroupBy(d => d.InspectorId)
            .Select(g => new
            {
                UserId = g.Key,
                Entries = g.Count(),
                TotalSeconds = g.Sum(d => (double)((d.TimeTo.Hour * 3600 + d.TimeTo.Minute * 60 + d.TimeTo.Second)
                                                 - (d.TimeFrom.Hour * 3600 + d.TimeFrom.Minute * 60 + d.TimeFrom.Second))),
            }).ToListAsync(ct);

        var allUserIds = byUser.Select(u => u.UserId)
            .Concat(dwrByUser.Select(u => u.UserId))
            .Distinct().ToList();

        var users = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => allUserIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .ToListAsync(ct);

        return allUserIds.Select(id =>
        {
            var c = byUser.FirstOrDefault(u => u.UserId == id);
            var d = dwrByUser.FirstOrDefault(u => u.UserId == id);
            var u = users.FirstOrDefault(x => x.Id == id);
            return new InspectorProductivityRowDto(
                id, u?.FullName ?? u?.UserName ?? id,
                c?.Created ?? 0, c?.Approved ?? 0,
                d?.Entries ?? 0,
                Math.Round((d?.TotalSeconds ?? 0) / 3600.0, 1));
        })
        .OrderByDescending(r => r.CertificatesApproved)
        .ToList();
    }
}

public sealed class GetDueSoonHandler : IQueryHandler<GetDueSoonQuery, IReadOnlyList<DueSoonRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public GetDueSoonHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<IReadOnlyList<DueSoonRowDto>> Handle(GetDueSoonQuery q, CancellationToken ct)
    {
        var days = Math.Clamp(q.Days, 1, 365);
        var today = DateOnly.FromDateTime(_clock.UtcNow);
        var threshold = today.AddDays(days);

        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();

        var rows = await certs
            .Where(c => c.NextDueDate != null && c.NextDueDate >= today && c.NextDueDate <= threshold)
            .Where(c => c.State == CertificateState.Approved
                     || c.State == CertificateState.ClientSent
                     || c.State == CertificateState.ClientAccepted)
            .OrderBy(c => c.NextDueDate)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new { c, cl })
            .Join(_db.Equipment.IgnoreQueryFilters(), x => x.c.EquipmentId, e => e.Id, (x, e) => new { x.c, x.cl, e })
            .Join(_db.EquipmentTypes, x => x.e.EquipmentTypeId, t => t.Id,
                (x, t) => new { x.c, x.cl, x.e, TypeName = t.Name })
            .Take(500)
            .Select(x => new DueSoonRowDto(
                x.c.CertificateNo, x.c.ClientId, x.cl.Name,
                x.e.IdNo, x.TypeName,
                x.c.NextDueDate!.Value,
                x.c.NextDueDate!.Value.DayNumber - today.DayNumber))
            .ToListAsync(ct);
        return rows;
    }
}

public sealed class GetOverdueHandler : IQueryHandler<GetOverdueQuery, IReadOnlyList<OverdueRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public GetOverdueHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<IReadOnlyList<OverdueRowDto>> Handle(GetOverdueQuery q, CancellationToken ct)
    {
        var today = DateOnly.FromDateTime(_clock.UtcNow);

        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();

        var rows = await certs
            .Where(c => c.NextDueDate != null && c.NextDueDate < today)
            .Where(c => c.State != CertificateState.Voided && c.State != CertificateState.Archived)
            .OrderBy(c => c.NextDueDate)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new { c, cl })
            .Join(_db.Equipment.IgnoreQueryFilters(), x => x.c.EquipmentId, e => e.Id, (x, e) => new { x.c, x.cl, e })
            .Take(500)
            .Select(x => new OverdueRowDto(
                x.c.CertificateNo, x.c.ClientId, x.cl.Name, x.e.IdNo,
                x.c.NextDueDate!.Value,
                today.DayNumber - x.c.NextDueDate!.Value.DayNumber))
            .ToListAsync(ct);
        return rows;
    }
}
