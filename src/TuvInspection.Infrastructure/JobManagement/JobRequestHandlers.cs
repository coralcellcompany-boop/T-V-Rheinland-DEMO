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
using TuvInspection.Domain.JobRequests;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.JobManagement;

public sealed class ListJobRequestsHandler : IQueryHandler<ListJobRequestsQuery, PagedResult<JobRequestListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListJobRequestsHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<JobRequestListItemDto>> Handle(ListJobRequestsQuery q, CancellationToken ct)
    {
        IQueryable<JobRequest> query = _tenant.IsInRole(Roles.Manager)
            ? _db.JobRequests.IgnoreQueryFilters().AsNoTracking()
            : _db.JobRequests.AsNoTracking();
        if (q.ClientId is { } cid) query = query.Where(r => r.ClientId == cid);
        if (q.Status is { } s) query = query.Where(r => r.Status == (JobRequestStatus)(int)s);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(r => EF.Functions.Like(r.RequestNo, $"%{q.Search.Trim()}%"));

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), r => r.ClientId, c => c.Id, (r, c) => new JobRequestListItemDto(
                r.Id, r.RequestNo, r.ClientId, c.Name,
                (int)r.Service, r.RequestedFrom, r.RequestedTo,
                r.Site, r.ContactEmail,
                (JobRequestStatusDto)r.Status, r.ConvertedJobOrderId, r.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<JobRequestListItemDto>(rows, total, page, pageSize);
    }
}

public sealed class GetJobRequestByIdHandler : IQueryHandler<GetJobRequestByIdQuery, JobRequestDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetJobRequestByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<JobRequestDetailDto?> Handle(GetJobRequestByIdQuery q, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.JobRequests.IgnoreQueryFilters().AsNoTracking()
            : _db.JobRequests.AsNoTracking();
        var r = await query.FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (r is null) return null;
        var name = await _db.Clients.IgnoreQueryFilters()
            .Where(c => c.Id == r.ClientId).Select(c => c.Name).FirstAsync(ct);
        return new JobRequestDetailDto(
            r.Id, r.RequestNo, r.ClientId, name,
            (int)r.Service, r.RequestedFrom, r.RequestedTo,
            r.Site, r.ContactName, r.ContactPhone, r.ContactEmail,
            r.ScopeNotes, r.PoReference,
            (JobRequestStatusDto)r.Status, r.ConvertedJobOrderId, r.RejectionReason,
            r.CreatedAtUtc, r.UpdatedAtUtc);
    }
}

public sealed class CreateJobRequestHandler : ICommandHandler<CreateJobRequestCommand, JobRequestDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly JobRequestNoGenerator _gen;
    public CreateJobRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock, JobRequestNoGenerator gen)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; }

    public async Task<JobRequestDetailDto> Handle(CreateJobRequestCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");
        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == command.Body.ClientId, ct);
        if (!clientExists) throw new ArgumentException("Unknown client.");

        var no = await _gen.Next(ct);
        var r = new JobRequest(Guid.NewGuid(), no, command.Body.ClientId,
            (ServiceType)command.Body.Service, command.Body.RequestedFrom, command.Body.RequestedTo);
        r.UpdateContact(command.Body.ContactName, command.Body.ContactPhone, command.Body.ContactEmail);
        r.UpdateScope(command.Body.Site, command.Body.ScopeNotes, command.Body.PoReference);
        r.CreatedAtUtc = _clock.UtcNow;
        r.CreatedById = _tenant.UserId;
        _db.JobRequests.Add(r);
        await _db.SaveChangesAsync(ct);
        return (await new GetJobRequestByIdHandler(_db, _tenant).Handle(new(r.Id), ct))!;
    }
}

public sealed class UpdateJobRequestHandler : ICommandHandler<UpdateJobRequestCommand, JobRequestDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public UpdateJobRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<JobRequestDetailDto> Handle(UpdateJobRequestCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobRequests.IgnoreQueryFilters() : _db.JobRequests;
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Job request {command.Id} not found.");

        // We don't expose request-no/from-to mutator; keep dates updateable through reconstruction.
        // For MVP we re-create using same id by adjusting via a simple update path.
        // Use existing methods on the aggregate:
        r.UpdateContact(command.Body.ContactName, command.Body.ContactPhone, command.Body.ContactEmail);
        r.UpdateScope(command.Body.Site, command.Body.ScopeNotes, command.Body.PoReference);
        // For dates/from-to, we update by re-loading via RawSql is overkill — extend aggregate later.
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetJobRequestByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class AcceptJobRequestHandler : ICommandHandler<AcceptJobRequestCommand, JobRequestDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public AcceptJobRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<JobRequestDetailDto> Handle(AcceptJobRequestCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");

        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobRequests.IgnoreQueryFilters() : _db.JobRequests;
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Job request {command.Id} not found.");
        if (r.Status == JobRequestStatus.New) r.BeginReview();
        r.Accept();
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetJobRequestByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class RejectJobRequestHandler : ICommandHandler<RejectJobRequestCommand, JobRequestDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public RejectJobRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<JobRequestDetailDto> Handle(RejectJobRequestCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");

        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobRequests.IgnoreQueryFilters() : _db.JobRequests;
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Job request {command.Id} not found.");
        r.Reject(command.Body.Reason);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetJobRequestByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class ConvertJobRequestHandler : ICommandHandler<ConvertJobRequestCommand, JobOrderDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly JobOrderNoGenerator _gen;
    public ConvertJobRequestHandler(AppDbContext db, ITenantContext tenant, IClock clock, JobOrderNoGenerator gen)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; }

    public async Task<JobOrderDetailDto> Handle(ConvertJobRequestCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Manager or Coordinator required.");

        var query = _tenant.IsInRole(Roles.Manager) ? _db.JobRequests.IgnoreQueryFilters() : _db.JobRequests;
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Job request {command.Id} not found.");

        var jobOrderNo = await _gen.Next(ct);
        var jo = new JobOrder(Guid.NewGuid(), jobOrderNo, r.ClientId, r.Service,
            r.RequestedFrom, r.RequestedTo);
        jo.UpdateLocation(r.Site);
        jo.CreatedAtUtc = _clock.UtcNow;
        jo.CreatedById = _tenant.UserId;
        _db.JobOrders.Add(jo);
        r.MarkConverted(jo.Id);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetJobOrderByIdHandler(_db, _tenant).Handle(new(jo.Id), ct))!;
    }
}
