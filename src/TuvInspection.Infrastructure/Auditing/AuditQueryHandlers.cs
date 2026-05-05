using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Auditing;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Auditing;
using TuvInspection.Contracts.Common;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Auditing;

/// <summary>
/// Reads from the audit DbContext (separate user with INSERT-only grant — but reads are
/// allowed via SELECT, which the dedicated DB user grants). Joins to the application DB
/// happen here at the application layer to attach the actor display name.
/// </summary>
public sealed class ListAuditHandler : IQueryHandler<ListAuditQuery, PagedResult<AuditLogRowDto>>
{
    private readonly AuditDbContext _audit;
    private readonly AppDbContext _app;
    public ListAuditHandler(AuditDbContext audit, AppDbContext app) { _audit = audit; _app = app; }

    public async Task<PagedResult<AuditLogRowDto>> Handle(ListAuditQuery q, CancellationToken ct)
    {
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var query = _audit.AuditLogs.AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q.EntityName))
            query = query.Where(a => a.EntityName == q.EntityName);
        if (!string.IsNullOrWhiteSpace(q.EntityId))
            query = query.Where(a => a.EntityId == q.EntityId);
        if (!string.IsNullOrWhiteSpace(q.ActorUserId))
            query = query.Where(a => a.ActorUserId == q.ActorUserId);
        if (q.FromUtc is { } from) query = query.Where(a => a.AtUtc >= from);
        if (q.ToUtc is { } to) query = query.Where(a => a.AtUtc <= to);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = $"%{q.Search.Trim()}%";
            query = query.Where(a =>
                EF.Functions.Like(a.EntityId, s) ||
                EF.Functions.Like(a.Action, s) ||
                (a.BeforeJson != null && EF.Functions.Like(a.BeforeJson, s)) ||
                (a.AfterJson != null && EF.Functions.Like(a.AfterJson, s)));
        }

        var total = await query.CountAsync(ct);

        var rows = await query
            .OrderByDescending(a => a.AtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(a => new
            {
                a.Id, a.EntityName, a.EntityId, a.Action, a.ActorUserId, a.ActorRole,
                a.AtUtc, a.Ip, a.BeforeJson, a.AfterJson, a.PreviousHash, a.CurrentHash,
            })
            .ToListAsync(ct);

        // Attach display name from the Identity store (only if we have an actor).
        var actorIds = rows.Where(r => r.ActorUserId is not null).Select(r => r.ActorUserId!).Distinct().ToList();
        var users = await _app.Users.AsNoTracking()
            .Where(u => actorIds.Contains(u.Id))
            .Select(u => new { u.Id, u.UserName, u.Email })
            .ToListAsync(ct);
        var nameById = users.ToDictionary(u => u.Id, u => u.UserName ?? u.Email ?? u.Id);

        var items = rows.Select(r => new AuditLogRowDto(
            r.Id, r.EntityName, r.EntityId, r.Action,
            r.ActorUserId,
            r.ActorUserId is not null && nameById.TryGetValue(r.ActorUserId, out var n) ? n : null,
            r.ActorRole,
            r.AtUtc, r.Ip,
            r.BeforeJson, r.AfterJson,
            r.PreviousHash, r.CurrentHash)).ToList();

        return new PagedResult<AuditLogRowDto>(items, total, page, pageSize);
    }
}
