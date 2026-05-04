using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.JobManagement;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.Timesheets;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.JobManagement;

public sealed class ListDwrHandler : IQueryHandler<ListDwrQuery, PagedResult<DwrListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListDwrHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<DwrListItemDto>> Handle(ListDwrQuery q, CancellationToken ct)
    {
        IQueryable<DailyWorkReport> query = _tenant.IsInRole(Roles.Manager)
            ? _db.DailyWorkReports.IgnoreQueryFilters().AsNoTracking()
            : _db.DailyWorkReports.AsNoTracking();

        // Inspectors only see their own DWRs by default.
        if (_tenant.IsInRole(Roles.Inspector) && !_tenant.IsInRole(Roles.Manager) && _tenant.UserId is not null)
            query = query.Where(d => d.InspectorId == _tenant.UserId);

        if (q.JobOrderId is { } jid) query = query.Where(d => d.JobOrderId == jid);
        if (!string.IsNullOrWhiteSpace(q.InspectorId)) query = query.Where(d => d.InspectorId == q.InspectorId);
        if (q.Status is { } s) query = query.Where(d => d.Status == (DwrStatus)(int)s);
        if (q.DateFrom is { } df) query = query.Where(d => d.Date >= df);
        if (q.DateTo is { } dt) query = query.Where(d => d.Date <= dt);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(d => EF.Functions.Like(d.DwrNo, $"%{q.Search.Trim()}%"));

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(d => d.Date).ThenByDescending(d => d.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.JobOrders.IgnoreQueryFilters(), d => d.JobOrderId, j => j.Id, (d, j) => new { d, j })
            .Join(_db.Clients.IgnoreQueryFilters(), x => x.d.ClientId, c => c.Id, (x, c) => new { x.d, x.j, c })
            .Join(_db.Users.IgnoreQueryFilters(), x => x.d.InspectorId, u => u.Id,
                (x, u) => new DwrListItemDto(
                    x.d.Id, x.d.DwrNo, x.d.JobOrderId, x.j.JobOrderNo,
                    x.d.ClientId, x.c.Name,
                    x.d.InspectorId, u.FullName ?? u.UserName,
                    x.d.Date, x.d.TimeFrom, x.d.TimeTo,
                    x.d.EquipmentInspected, x.d.OperatorsAssessed,
                    (DwrStatusDto)x.d.Status, x.d.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<DwrListItemDto>(rows, total, page, pageSize);
    }
}

public sealed class GetDwrByIdHandler : IQueryHandler<GetDwrByIdQuery, DwrDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetDwrByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<DwrDetailDto?> Handle(GetDwrByIdQuery q, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.DailyWorkReports.IgnoreQueryFilters().AsNoTracking()
            : _db.DailyWorkReports.AsNoTracking();
        var d = await query.FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (d is null) return null;
        var jobNo = await _db.JobOrders.IgnoreQueryFilters().Where(j => j.Id == d.JobOrderId)
            .Select(j => j.JobOrderNo).FirstAsync(ct);
        var clientName = await _db.Clients.IgnoreQueryFilters().Where(c => c.Id == d.ClientId)
            .Select(c => c.Name).FirstAsync(ct);
        var inspectorName = await _db.Users.IgnoreQueryFilters().Where(u => u.Id == d.InspectorId)
            .Select(u => u.FullName ?? u.UserName).FirstOrDefaultAsync(ct);

        return new DwrDetailDto(
            d.Id, d.DwrNo, d.JobOrderId, jobNo,
            d.ClientId, clientName,
            d.InspectorId, inspectorName,
            d.Date, d.TimeFrom, d.TimeTo,
            d.Location, d.EquipmentInspected, d.OperatorsAssessed,
            d.Notes, (DwrStatusDto)d.Status, d.RejectionReason,
            d.CreatedAtUtc, d.UpdatedAtUtc);
    }
}

public sealed class CreateDwrHandler : ICommandHandler<CreateDwrCommand, DwrDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly DwrNoGenerator _gen;
    public CreateDwrHandler(AppDbContext db, ITenantContext tenant, IClock clock, DwrNoGenerator gen)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; }

    public async Task<DwrDetailDto> Handle(CreateDwrCommand command, CancellationToken ct)
    {
        if (_tenant.UserId is null) throw new UnauthorizedAccessException("Login required.");

        var jo = await _db.JobOrders.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(j => j.Id == command.Body.JobOrderId, ct)
            ?? throw new ArgumentException("Job order not found.");

        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(jo.ClientId))
            throw new UnauthorizedAccessException("Job order is not under one of your clients.");

        var no = await _gen.Next(ct);
        var d = new DailyWorkReport(Guid.NewGuid(), no, jo.Id, jo.ClientId,
            _tenant.UserId, command.Body.Date, command.Body.TimeFrom, command.Body.TimeTo);
        d.UpdateDetails(command.Body.Location, command.Body.EquipmentInspected,
            command.Body.OperatorsAssessed, command.Body.Notes);
        d.CreatedAtUtc = _clock.UtcNow;
        d.CreatedById = _tenant.UserId;
        _db.DailyWorkReports.Add(d);
        await _db.SaveChangesAsync(ct);
        return (await new GetDwrByIdHandler(_db, _tenant).Handle(new(d.Id), ct))!;
    }
}

public sealed class UpdateDwrHandler : ICommandHandler<UpdateDwrCommand, DwrDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public UpdateDwrHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<DwrDetailDto> Handle(UpdateDwrCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.DailyWorkReports.IgnoreQueryFilters() : _db.DailyWorkReports;
        var d = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"DWR {command.Id} not found.");
        // Only the owning inspector or a manager may edit.
        if (!_tenant.IsInRole(Roles.Manager) && d.InspectorId != _tenant.UserId)
            throw new UnauthorizedAccessException("You can only edit your own DWRs.");

        d.UpdateDetails(command.Body.Location, command.Body.EquipmentInspected,
            command.Body.OperatorsAssessed, command.Body.Notes);
        d.UpdatedAtUtc = _clock.UtcNow;
        d.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetDwrByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class SubmitDwrHandler : ICommandHandler<SubmitDwrCommand, DwrDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public SubmitDwrHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<DwrDetailDto> Handle(SubmitDwrCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.DailyWorkReports.IgnoreQueryFilters() : _db.DailyWorkReports;
        var d = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"DWR {command.Id} not found.");
        if (!_tenant.IsInRole(Roles.Manager) && d.InspectorId != _tenant.UserId)
            throw new UnauthorizedAccessException("You can only submit your own DWRs.");
        d.Submit();
        d.UpdatedAtUtc = _clock.UtcNow;
        d.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetDwrByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class ApproveDwrHandler : ICommandHandler<ApproveDwrCommand, DwrDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public ApproveDwrHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<DwrDetailDto> Handle(ApproveDwrCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");
        var query = _tenant.IsInRole(Roles.Manager) ? _db.DailyWorkReports.IgnoreQueryFilters() : _db.DailyWorkReports;
        var d = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"DWR {command.Id} not found.");
        d.Approve();
        d.UpdatedAtUtc = _clock.UtcNow;
        d.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetDwrByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class RejectDwrHandler : ICommandHandler<RejectDwrCommand, DwrDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public RejectDwrHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<DwrDetailDto> Handle(RejectDwrCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");
        var query = _tenant.IsInRole(Roles.Manager) ? _db.DailyWorkReports.IgnoreQueryFilters() : _db.DailyWorkReports;
        var d = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"DWR {command.Id} not found.");
        d.Reject(command.Body.Reason);
        d.UpdatedAtUtc = _clock.UtcNow;
        d.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetDwrByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}
