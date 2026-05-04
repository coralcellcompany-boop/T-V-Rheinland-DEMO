using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.JobManagement;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.JobOrders;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.JobManagement;

internal static class JobOrderMapper
{
    public static JobOrderListItemDto ToListItem(JobOrder j, string clientName) =>
        new(j.Id, j.JobOrderNo, j.ClientId, clientName,
            (ServiceTypeDto)(int)j.Service, j.DateFrom, j.DateTo,
            j.Location, (JobOrderStatusDto)j.Status,
            j.AssignedInspectorIds.Count, j.CreatedAtUtc);

    public static JobOrderDetailDto ToDetail(JobOrder j, string clientName) =>
        new(j.Id, j.JobOrderNo, j.ClientId, clientName,
            (ServiceTypeDto)(int)j.Service, j.DateFrom, j.DateTo,
            j.Location, (JobOrderStatusDto)j.Status,
            j.AssignedInspectorIds.ToList(),
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

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(j => j.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), j => j.ClientId, c => c.Id, (j, c) => new { j, c })
            .ToListAsync(ct);

        var items = rows.Select(x => JobOrderMapper.ToListItem(x.j, x.c.Name)).ToList();
        return new PagedResult<JobOrderListItemDto>(items, total, page, pageSize);
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

        var no = await _gen.Next(ct);
        var jo = new JobOrder(Guid.NewGuid(), no, command.Body.ClientId,
            (ServiceType)(int)command.Body.Service, command.Body.DateFrom, command.Body.DateTo);
        jo.UpdateLocation(command.Body.Location);
        jo.CreatedAtUtc = _clock.UtcNow;
        jo.CreatedById = _tenant.UserId;
        _db.JobOrders.Add(jo);
        await _db.SaveChangesAsync(ct);
        return (await new GetJobOrderByIdHandler(_db, _tenant).Handle(new(jo.Id), ct))!;
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
