using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.Stickers;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Stickers;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.Stickers;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Stickers;

public sealed class ListStickerRequestsHandler
    : IQueryHandler<ListStickerRequestsQuery, PagedResult<StickerRequestDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListStickerRequestsHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<PagedResult<StickerRequestDto>> Handle(ListStickerRequestsQuery q, CancellationToken ct)
    {
        IQueryable<StickerRequest> query = _db.StickerRequests.AsNoTracking();

        // Inspectors only see their own requests; Coordinator/Manager see everything.
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            query = query.Where(r => r.InspectorUserId == _tenant.UserId);

        if (q.State is { } s) query = query.Where(r => r.State == (StickerRequestState)(int)s);
        if (!string.IsNullOrWhiteSpace(q.InspectorUserId))
            query = query.Where(r => r.InspectorUserId == q.InspectorUserId);

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new
            {
                r.Id, r.RequestNo, r.InspectorUserId, r.Color, r.Quantity, r.Justification,
                r.State, r.DecidedByUserId, r.DecidedAtUtc, r.DecisionComments,
                r.AllocatedCount, r.CreatedAtUtc,
                InspectorName = _db.Users.Where(u => u.Id == r.InspectorUserId)
                    .Select(u => u.FullName ?? u.UserName ?? u.Email).FirstOrDefault(),
                DecidedByName = _db.Users.Where(u => u.Id == r.DecidedByUserId)
                    .Select(u => u.FullName ?? u.UserName ?? u.Email).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new StickerRequestDto(
            r.Id, r.RequestNo,
            r.InspectorUserId, r.InspectorName,
            (StickerColorDto)r.Color, r.Quantity, r.Justification,
            (StickerRequestStateDto)r.State,
            r.DecidedByUserId, r.DecidedByName, r.DecidedAtUtc, r.DecisionComments,
            r.AllocatedCount, r.CreatedAtUtc)).ToList();

        return new PagedResult<StickerRequestDto>(items, total, page, pageSize);
    }
}

public sealed class CreateStickerRequestHandler
    : ICommandHandler<CreateStickerRequestCommand, StickerRequestDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateStickerRequest> _validator;
    public CreateStickerRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<CreateStickerRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<StickerRequestDto> Handle(CreateStickerRequestCommand c, CancellationToken ct)
    {
        if (string.IsNullOrEmpty(_tenant.UserId))
            throw new UnauthorizedAccessException("Sign-in required to request stickers.");

        // Anyone except a pure ClientUser can submit a request — Inspectors are the primary use
        // case but Coordinators may also pre-allocate stock to themselves.
        if (_tenant.IsInRole(Roles.ClientUser) && !_tenant.IsInRole(Roles.Inspector))
            throw new UnauthorizedAccessException("Client users cannot request stickers.");

        await _validator.ValidateAndThrowAsync(c.Body, ct);

        var year = _clock.UtcNow.Year;
        var lastNo = await _db.StickerRequests
            .Where(r => r.RequestNo.StartsWith($"SR-{year}-"))
            .OrderByDescending(r => r.RequestNo)
            .Select(r => r.RequestNo)
            .FirstOrDefaultAsync(ct);
        var seq = 1;
        if (lastNo is not null && int.TryParse(lastNo[^4..], out var n)) seq = n + 1;
        var requestNo = $"SR-{year}-{seq:D4}";

        var entity = new StickerRequest(
            Guid.NewGuid(), requestNo, _tenant.UserId,
            (StickerColor)(int)c.Body.Color, c.Body.Quantity, c.Body.Justification);
        entity.CreatedAtUtc = _clock.UtcNow;
        entity.CreatedById = _tenant.UserId;

        _db.StickerRequests.Add(entity);
        await _db.SaveChangesAsync(ct);

        return await StickerRequestHandlerHelpers.ProjectAsync(_db, entity.Id, ct);
    }
}

public sealed class ApproveStickerRequestHandler
    : ICommandHandler<ApproveStickerRequestCommand, StickerRequestDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOutbox _outbox;
    public ApproveStickerRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock, IOutbox outbox)
    { _db = db; _tenant = tenant; _clock = clock; _outbox = outbox; }

    public async Task<StickerRequestDto> Handle(ApproveStickerRequestCommand c, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Only Manager or Coordinator may approve sticker requests.");

        var r = await _db.StickerRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
            ?? throw new KeyNotFoundException($"Sticker request {c.Id} not found.");

        // Allocate from pool (matching colour, oldest first). Approve regardless of how many
        // we managed to allocate — record the count so the inspector and the approver can
        // see the gap and procure more stock if needed.
        var pool = await _db.Stickers
            .Where(s => s.State == StickerState.Unallocated
                && s.Color == r.Color
                && s.AssignedToInspectorId == null)
            .OrderBy(s => s.CreatedAtUtc)
            .Take(r.Quantity)
            .ToListAsync(ct);

        foreach (var s in pool)
        {
            s.AssignToInspector(r.InspectorUserId, r.Id, _clock.UtcNow);
            s.UpdatedAtUtc = _clock.UtcNow;
            s.UpdatedById = _tenant.UserId;
        }

        r.Approve(_tenant.UserId!, c.Comments, pool.Count, _clock.UtcNow);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;

        await _outbox.Enqueue(new StickerRequestDecidedEmail(
            r.Id, r.RequestNo, r.InspectorUserId, true,
            r.Quantity, pool.Count, c.Comments, _clock.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
        return await StickerRequestHandlerHelpers.ProjectAsync(_db, r.Id, ct);
    }
}

public sealed class RejectStickerRequestHandler
    : ICommandHandler<RejectStickerRequestCommand, StickerRequestDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOutbox _outbox;
    public RejectStickerRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock, IOutbox outbox)
    { _db = db; _tenant = tenant; _clock = clock; _outbox = outbox; }

    public async Task<StickerRequestDto> Handle(RejectStickerRequestCommand c, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Only Manager or Coordinator may reject sticker requests.");
        if (string.IsNullOrWhiteSpace(c.Reason))
            throw new ArgumentException("A rejection reason is required.");

        var r = await _db.StickerRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
            ?? throw new KeyNotFoundException($"Sticker request {c.Id} not found.");
        r.Reject(_tenant.UserId!, c.Reason, _clock.UtcNow);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;

        await _outbox.Enqueue(new StickerRequestDecidedEmail(
            r.Id, r.RequestNo, r.InspectorUserId, false,
            r.Quantity, 0, c.Reason, _clock.UtcNow), ct);

        await _db.SaveChangesAsync(ct);
        return await StickerRequestHandlerHelpers.ProjectAsync(_db, r.Id, ct);
    }
}

/// <summary>
/// Outbox payload: notify the inspector when their sticker request was approved or rejected.
/// </summary>
public sealed record StickerRequestDecidedEmail(
    Guid RequestId,
    string RequestNo,
    string InspectorUserId,
    bool Approved,
    int RequestedQuantity,
    int AllocatedCount,
    string? Comments,
    DateTime AtUtc);

public sealed class CancelStickerRequestHandler
    : ICommandHandler<CancelStickerRequestCommand, StickerRequestDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public CancelStickerRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<StickerRequestDto> Handle(CancelStickerRequestCommand c, CancellationToken ct)
    {
        var r = await _db.StickerRequests.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
            ?? throw new KeyNotFoundException($"Sticker request {c.Id} not found.");

        // Owner of the request, or a Manager, may cancel.
        if (r.InspectorUserId != _tenant.UserId && !_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only the requester or a Manager may cancel.");

        r.Cancel(_clock.UtcNow);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return await StickerRequestHandlerHelpers.ProjectAsync(_db, r.Id, ct);
    }
}

internal static class StickerRequestHandlerHelpers
{
    public static async Task<StickerRequestDto> ProjectAsync(AppDbContext db, Guid id, CancellationToken ct)
    {
        var row = await db.StickerRequests.AsNoTracking()
            .Where(r => r.Id == id)
            .Select(r => new
            {
                r.Id, r.RequestNo, r.InspectorUserId, r.Color, r.Quantity, r.Justification,
                r.State, r.DecidedByUserId, r.DecidedAtUtc, r.DecisionComments,
                r.AllocatedCount, r.CreatedAtUtc,
                InspectorName = db.Users.Where(u => u.Id == r.InspectorUserId)
                    .Select(u => u.FullName ?? u.UserName ?? u.Email).FirstOrDefault(),
                DecidedByName = db.Users.Where(u => u.Id == r.DecidedByUserId)
                    .Select(u => u.FullName ?? u.UserName ?? u.Email).FirstOrDefault(),
            })
            .FirstAsync(ct);

        return new StickerRequestDto(
            row.Id, row.RequestNo,
            row.InspectorUserId, row.InspectorName,
            (StickerColorDto)row.Color, row.Quantity, row.Justification,
            (StickerRequestStateDto)row.State,
            row.DecidedByUserId, row.DecidedByName, row.DecidedAtUtc, row.DecisionComments,
            row.AllocatedCount, row.CreatedAtUtc);
    }
}
