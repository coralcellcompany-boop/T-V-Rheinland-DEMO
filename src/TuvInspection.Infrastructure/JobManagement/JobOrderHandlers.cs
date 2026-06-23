using FluentValidation;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.JobManagement;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.JobOrders;
using TuvInspection.Infrastructure.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.JobManagement;

internal static class JobOrderMapper
{
    public static JobOrderListItemDto ToListItem(JobOrder j, string clientName) =>
        new(j.Id, j.JobOrderNo, j.ClientId, clientName,
            (ServiceTypeDto)(int)j.Service, j.DateFrom, j.DateTo,
            j.Location, (JobOrderStatusDto)j.Status,
            j.AssignedInspectorIds.Count, j.AttachmentKeys.Count, j.CreatedAtUtc);

    public static JobOrderDetailDto ToDetail(JobOrder j, string clientName) =>
        new(j.Id, j.JobOrderNo, j.ClientId, clientName,
            (ServiceTypeDto)(int)j.Service, j.DateFrom, j.DateTo,
            j.Location, (JobOrderStatusDto)j.Status,
            j.AssignedInspectorIds.ToList(),
            j.AttachmentKeys.ToList(),
            j.CreatedAtUtc, j.UpdatedAtUtc);
}

public sealed class ListJobOrdersHandler : IQueryHandler<ListJobOrdersQuery, PagedResult<JobOrderListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListJobOrdersHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<JobOrderListItemDto>> Handle(ListJobOrdersQuery q, CancellationToken ct)
    {
        IQueryable<JobOrder> query = _tenant.IsInRole(Roles.Manager)
            ? _db.JobOrders.IgnoreQueryFilters().AsNoTracking()
            : _db.JobOrders.AsNoTracking();
        if (q.ClientId is { } cid) query = query.Where(j => j.ClientId == cid);
        if (q.Status is { } st) query = query.Where(j => j.Status == (JobOrderStatus)(int)st);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(j => EF.Functions.Like(j.JobOrderNo, $"%{q.Search.Trim()}%"));

        // Inspector-scoped filter: either an explicit AssignedInspectorId param (used by
        // Manager/Coordinator dashboards) or MineOnly which pins it to the caller (used
        // by inspectors viewing their own queue). The inspector list lives in a JSON
        // column with a value converter, so EF cannot translate Contains to SQL — we
        // materialize the page and apply the filter in memory. Pragmatic at MVP volumes;
        // a backing CSV/junction table would scale better.
        var inspectorId = q.MineOnly ? _tenant.UserId : q.AssignedInspectorId;
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        if (!string.IsNullOrWhiteSpace(inspectorId))
        {
            var all = await query.OrderByDescending(j => j.CreatedAtUtc).ToListAsync(ct);
            var filtered = all.Where(j => j.AssignedInspectorIds.Contains(inspectorId)).ToList();
            var pageItems = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList();

            var clientIds = pageItems.Select(j => j.ClientId).Distinct().ToList();
            var clientNames = await _db.Clients.IgnoreQueryFilters()
                .Where(c => clientIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id, c => c.Name, ct);

            var items = pageItems.Select(j => JobOrderMapper.ToListItem(j,
                clientNames.GetValueOrDefault(j.ClientId, ""))).ToList();
            return new PagedResult<JobOrderListItemDto>(items, filtered.Count, page, pageSize);
        }

        var total = await query.CountAsync(ct);
        var rows = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), j => j.ClientId, c => c.Id, (j, c) => new { j, c })
            .ToListAsync(ct);
        var unfilteredItems = rows.Select(x => JobOrderMapper.ToListItem(x.j, x.c.Name)).ToList();
        return new PagedResult<JobOrderListItemDto>(unfilteredItems, total, page, pageSize);
    }
}

public sealed class GetJobOrderByIdHandler : IQueryHandler<GetJobOrderByIdQuery, JobOrderDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetJobOrderByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<JobOrderDetailDto?> Handle(GetJobOrderByIdQuery q, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.JobOrders.IgnoreQueryFilters().AsNoTracking()
            : _db.JobOrders.AsNoTracking();
        var j = await query.FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (j is null) return null;
        var name = await _db.Clients.IgnoreQueryFilters()
            .Where(c => c.Id == j.ClientId).Select(c => c.Name).FirstAsync(ct);
        return JobOrderMapper.ToDetail(j, name);
    }
}

public sealed class CreateJobOrderHandler : ICommandHandler<CreateJobOrderCommand, JobOrderDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly JobOrderNoGenerator _gen;

    public CreateJobOrderHandler(AppDbContext db, ITenantContext tenant, IClock clock, JobOrderNoGenerator gen)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; }

    public async Task<JobOrderDetailDto> Handle(CreateJobOrderCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(command.Body.ClientId))
            throw new UnauthorizedAccessException("You can only create orders for clients you are assigned to.");

        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == command.Body.ClientId, ct);
        if (!clientExists) throw new ArgumentException("Unknown client.");

        // Comment #1: a quantity > 1 creates that many SEPARATE job orders, each with its own
        // auto number. We save per-iteration so the JobOrderNoGenerator (which counts existing
        // rows) sees the previously-created order and never collides.
        var quantity = Math.Clamp(command.Body.Quantity <= 0 ? 1 : command.Body.Quantity, 1, 50);
        var attachments = command.Body.AttachmentKeys ?? new List<string>();

        JobOrder? first = null;
        for (var i = 0; i < quantity; i++)
        {
            var no = await _gen.Next(ct);
            var jo = new JobOrder(Guid.NewGuid(), no, command.Body.ClientId,
                (ServiceType)(int)command.Body.Service, command.Body.DateFrom, command.Body.DateTo);
            jo.UpdateLocation(command.Body.Location);
            jo.SetAttachments(attachments);
            jo.CreatedAtUtc = _clock.UtcNow;
            jo.CreatedById = _tenant.UserId;
            _db.JobOrders.Add(jo);
            await _db.SaveChangesAsync(ct);
            first ??= jo;
        }

        return (await new GetJobOrderByIdHandler(_db, _tenant).Handle(new(first!.Id), ct))!;
    }
}

public sealed class UpdateJobOrderHandler : ICommandHandler<UpdateJobOrderCommand, JobOrderDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public UpdateJobOrderHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<JobOrderDetailDto> Handle(UpdateJobOrderCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");

        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobOrders.IgnoreQueryFilters() : _db.JobOrders;
        var jo = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Job order {command.Id} not found.");

        jo.UpdateLocation(command.Body.Location);
        if (command.Body.AttachmentKeys is { } keys) jo.SetAttachments(keys);
        // Status transitions
        switch (command.Body.Status)
        {
            case JobOrderStatusDto.InProgress: if (jo.Status == JobOrderStatus.Open) jo.Begin(); break;
            case JobOrderStatusDto.Completed:  jo.Complete(); break;
            case JobOrderStatusDto.Cancelled:  jo.Cancel(); break;
        }
        // Sync inspector assignments
        var current = jo.AssignedInspectorIds.ToHashSet();
        var requested = command.Body.AssignedInspectorIds.ToHashSet();
        foreach (var u in current.Except(requested)) jo.UnassignInspector(u);
        foreach (var u in requested.Except(current)) jo.AssignInspector(u);

        jo.UpdatedAtUtc = _clock.UtcNow;
        jo.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetJobOrderByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

/// <summary>
/// Status-flip commands. Inspectors can only flip a JO they are assigned to
/// (Begin / Complete) — the actual physical site work; Manager/Coordinator can do any
/// flip including Cancel.
/// </summary>
public abstract class JobOrderStatusFlipHandlerBase
{
    protected readonly AppDbContext _db;
    protected readonly ITenantContext _tenant;
    protected readonly IClock _clock;
    protected JobOrderStatusFlipHandlerBase(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    protected async Task<JobOrder> LoadAndAuthorize(Guid id, bool inspectorAllowed, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobOrders.IgnoreQueryFilters() : _db.JobOrders;
        var jo = await query.FirstOrDefaultAsync(x => x.Id == id, ct)
            ?? throw new KeyNotFoundException($"Job order {id} not found.");

        var isStaff = _tenant.IsInRole(Roles.Manager) || _tenant.IsInRole(Roles.Coordinator);
        if (isStaff) return jo;

        if (!inspectorAllowed || _tenant.UserId is null
            || !jo.AssignedInspectorIds.Contains(_tenant.UserId))
            throw new UnauthorizedAccessException(
                "Only Manager/Coordinator or an assigned Inspector can change this job order's status.");
        return jo;
    }

    protected async Task<JobOrderDetailDto> Persist(JobOrder jo, CancellationToken ct)
    {
        jo.UpdatedAtUtc = _clock.UtcNow;
        jo.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetJobOrderByIdHandler(_db, _tenant).Handle(new(jo.Id), ct))!;
    }
}

public sealed class BeginJobOrderHandler : JobOrderStatusFlipHandlerBase, ICommandHandler<BeginJobOrderCommand, JobOrderDetailDto>
{
    public BeginJobOrderHandler(AppDbContext db, ITenantContext tenant, IClock clock) : base(db, tenant, clock) { }
    public async Task<JobOrderDetailDto> Handle(BeginJobOrderCommand c, CancellationToken ct)
    {
        var jo = await LoadAndAuthorize(c.Id, inspectorAllowed: true, ct);
        if (jo.Status != JobOrderStatus.Open)
            throw new InvalidOperationException($"Cannot begin a job order in state {jo.Status}.");
        jo.Begin();
        return await Persist(jo, ct);
    }
}

public sealed class CompleteJobOrderHandler : JobOrderStatusFlipHandlerBase, ICommandHandler<CompleteJobOrderCommand, JobOrderDetailDto>
{
    public CompleteJobOrderHandler(AppDbContext db, ITenantContext tenant, IClock clock) : base(db, tenant, clock) { }
    public async Task<JobOrderDetailDto> Handle(CompleteJobOrderCommand c, CancellationToken ct)
    {
        var jo = await LoadAndAuthorize(c.Id, inspectorAllowed: true, ct);
        if (jo.Status is JobOrderStatus.Completed or JobOrderStatus.Cancelled)
            throw new InvalidOperationException($"Job order is already in terminal state {jo.Status}.");
        jo.Complete();
        return await Persist(jo, ct);
    }
}

public sealed class CancelJobOrderHandler : JobOrderStatusFlipHandlerBase, ICommandHandler<CancelJobOrderCommand, JobOrderDetailDto>
{
    public CancelJobOrderHandler(AppDbContext db, ITenantContext tenant, IClock clock) : base(db, tenant, clock) { }
    public async Task<JobOrderDetailDto> Handle(CancelJobOrderCommand c, CancellationToken ct)
    {
        // Inspectors should not cancel — only staff.
        var jo = await LoadAndAuthorize(c.Id, inspectorAllowed: false, ct);
        if (jo.Status is JobOrderStatus.Completed or JobOrderStatus.Cancelled)
            throw new InvalidOperationException($"Job order is already in terminal state {jo.Status}.");
        jo.Cancel();
        return await Persist(jo, ct);
    }
}

/// <summary>
/// Picks the least-loaded active Inspector eligible to work on this Job Order's client and
/// adds them to <c>AssignedInspectorIds</c>. "Least loaded" = fewest in-flight certificates
/// (Draft / Submitted / UnderReview). Eligibility = active Inspector with the client in their
/// assigned-clients CSV (or any Inspector if AssignedClientIdsCsv is empty, since that means
/// the user has cross-client scope).
/// </summary>
public sealed class AutoAssignJobOrderInspectorHandler
    : ICommandHandler<AutoAssignJobOrderInspectorCommand, JobOrderDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly UserManager<ApplicationUser> _users;

    public AutoAssignJobOrderInspectorHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        UserManager<ApplicationUser> users)
    { _db = db; _tenant = tenant; _clock = clock; _users = users; }

    public async Task<JobOrderDetailDto> Handle(AutoAssignJobOrderInspectorCommand c, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");

        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobOrders.IgnoreQueryFilters() : _db.JobOrders;
        var jo = await query.FirstOrDefaultAsync(x => x.Id == c.Id, ct)
            ?? throw new KeyNotFoundException($"Job order {c.Id} not found.");

        var inspectorUsers = await _users.GetUsersInRoleAsync(Roles.Inspector);
        var clientIdString = jo.ClientId.ToString();

        // Eligibility filter: must be active, and either no client scope (cross-client) or
        // explicitly assigned to this client.
        var eligible = inspectorUsers
            .Where(u => u.IsActive)
            .Where(u => string.IsNullOrWhiteSpace(u.AssignedClientIdsCsv)
                || u.AssignedClientIdsCsv
                    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                    .Contains(clientIdString, StringComparer.OrdinalIgnoreCase))
            .Where(u => !jo.AssignedInspectorIds.Contains(u.Id))   // skip already-assigned
            .ToList();

        if (eligible.Count == 0)
            throw new InvalidOperationException(
                "No eligible inspector found for this client. Assign one manually or grant a user the Inspector role and client scope.");

        // Workload = open certs created by this user that haven't reached a terminal state.
        var inflightActive = new[]
        {
            CertificateState.Draft,
            CertificateState.Submitted,
            CertificateState.UnderReview,
            CertificateState.AwaitingApproval,
        };
        var eligibleIds = eligible.Select(u => u.Id).ToList();
        var loads = await _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            .Where(cert => cert.CreatedById != null
                && eligibleIds.Contains(cert.CreatedById)
                && inflightActive.Contains(cert.State))
            .GroupBy(cert => cert.CreatedById!)
            .Select(g => new { UserId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(g => g.UserId, g => g.Count, ct);

        // Pick the inspector with the smallest in-flight load; tie-break by username for
        // deterministic behaviour in tests.
        var pick = eligible
            .OrderBy(u => loads.GetValueOrDefault(u.Id, 0))
            .ThenBy(u => u.UserName, StringComparer.OrdinalIgnoreCase)
            .First();

        jo.AssignInspector(pick.Id);
        jo.UpdatedAtUtc = _clock.UtcNow;
        jo.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);

        return (await new GetJobOrderByIdHandler(_db, _tenant).Handle(new(c.Id), ct))!;
    }
}
