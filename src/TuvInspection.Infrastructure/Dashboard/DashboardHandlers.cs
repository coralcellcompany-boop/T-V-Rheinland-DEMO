using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.Dashboard;
using TuvInspection.Contracts.Certificates;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Dashboard;

public sealed class GetDashboardKpisHandler : IQueryHandler<GetDashboardKpisQuery, DashboardKpisDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public GetDashboardKpisHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db; _tenant = tenant; _clock = clock;
    }

    public async Task<DashboardKpisDto> Handle(GetDashboardKpisQuery q, CancellationToken ct)
    {
        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters()
            : _db.Certificates;
        if (q.ClientId is { } cid) certs = certs.Where(c => c.ClientId == cid);

        var now = _clock.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var dueThreshold = DateOnly.FromDateTime(now.AddDays(30));

        var total = await certs.CountAsync(ct);
        var thisMonth = await certs.CountAsync(c => c.CreatedAtUtc >= monthStart, ct);
        var pending = await certs.CountAsync(c =>
            c.State == CertificateState.Submitted ||
            c.State == CertificateState.UnderReview ||
            c.State == CertificateState.AwaitingApproval, ct);
        var rejected = await certs.CountAsync(c => c.State == CertificateState.Rejected, ct);
        var dueSoon = await certs.CountAsync(c =>
            c.State == CertificateState.Approved &&
            c.NextDueDate != null &&
            c.NextDueDate <= dueThreshold, ct);
        var expired = await certs.CountAsync(c => c.State == CertificateState.Expired, ct);

        IQueryable<TuvInspection.Domain.Equipment.Equipment> equipQ = _tenant.IsInRole(Roles.Manager)
            ? _db.Equipment.IgnoreQueryFilters()
            : _db.Equipment;
        if (q.ClientId is { } cid2) equipQ = equipQ.Where(e => e.ClientId == cid2);
        var activeEquipment = await equipQ.CountAsync(e => e.Status == EquipmentStatus.Active, ct);

        var clients = _tenant.IsInRole(Roles.Manager)
            ? await _db.Clients.CountAsync(ct)
            : _tenant.AssignedClientIds.Count;

        return new DashboardKpisDto(total, thisMonth, pending, rejected, dueSoon, expired,
            activeEquipment, clients);
    }
}

/// <summary>
/// Per-inspector dashboard analysis (Ahmed dashboard notes): distinct equipment handled,
/// distinct companies covered, and certificate throughput per inspector over a window.
/// </summary>
public sealed class GetInspectorAnalysisHandler
    : IQueryHandler<GetInspectorAnalysisQuery, IReadOnlyList<InspectorAnalysisRowDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public GetInspectorAnalysisHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<IReadOnlyList<InspectorAnalysisRowDto>> Handle(
        GetInspectorAnalysisQuery q, CancellationToken ct)
    {
        var days = Math.Clamp(q.Days <= 0 ? 90 : q.Days, 1, 365);
        var since = _clock.UtcNow.AddDays(-days);

        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();
        certs = certs.Where(c => c.CreatedById != null && c.CreatedAtUtc >= since);

        var grouped = await certs
            .GroupBy(c => c.CreatedById!)
            .Select(g => new
            {
                UserId = g.Key,
                EquipmentCount = g.Select(c => c.EquipmentId).Distinct().Count(),
                CompaniesCount = g.Select(c => c.ClientId).Distinct().Count(),
                Created = g.Count(),
                Approved = g.Count(c => c.State >= CertificateState.Approved
                                     && c.State != CertificateState.Rejected
                                     && c.State != CertificateState.Voided),
            }).ToListAsync(ct);

        var ids = grouped.Select(g => g.UserId).ToList();
        var users = await _db.Users.IgnoreQueryFilters().AsNoTracking()
            .Where(u => ids.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.FullName })
            .ToListAsync(ct);

        return grouped.Select(g =>
        {
            var u = users.FirstOrDefault(x => x.Id == g.UserId);
            return new InspectorAnalysisRowDto(
                g.UserId, u?.FullName ?? u?.UserName ?? g.UserId,
                g.EquipmentCount, g.CompaniesCount, g.Created, g.Approved);
        })
        .OrderByDescending(r => r.EquipmentCount)
        .ToList();
    }
}

public sealed class GetRecentActivityHandler : IQueryHandler<GetRecentActivityQuery, IReadOnlyList<RecentActivityItemDto>>
{
    private readonly AuditDbContext _audit;
    public GetRecentActivityHandler(AuditDbContext audit) => _audit = audit;

    public async Task<IReadOnlyList<RecentActivityItemDto>> Handle(GetRecentActivityQuery q, CancellationToken ct)
    {
        var limit = Math.Clamp(q.Limit, 1, 100);
        var rows = await _audit.AuditLogs.AsNoTracking()
            .OrderByDescending(a => a.AtUtc)
            .Take(limit)
            .Select(a => new RecentActivityItemDto(
                a.EntityName, a.EntityId, a.Action, a.ActorUserId, a.ActorRole, a.AtUtc))
            .ToListAsync(ct);
        return rows;
    }
}
