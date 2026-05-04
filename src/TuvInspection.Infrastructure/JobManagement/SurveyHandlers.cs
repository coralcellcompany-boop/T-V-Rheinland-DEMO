using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.JobManagement;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.Surveys;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.JobManagement;

public sealed class ListSurveysHandler : IQueryHandler<ListSurveysQuery, PagedResult<SurveyListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListSurveysHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<SurveyListItemDto>> Handle(ListSurveysQuery q, CancellationToken ct)
    {
        IQueryable<Survey> query = _tenant.IsInRole(Roles.Manager)
            ? _db.Surveys.IgnoreQueryFilters().AsNoTracking()
            : _db.Surveys.AsNoTracking();

        if (q.ClientId is { } cid) query = query.Where(s => s.ClientId == cid);
        if (q.Status is { } s) query = query.Where(x => x.Status == (SurveyStatus)(int)s);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(s => EF.Functions.Like(s.SurveyNo, $"%{q.Search.Trim()}%"));

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(s => s.Date)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), s => s.ClientId, c => c.Id, (s, c) => new SurveyListItemDto(
                s.Id, s.SurveyNo, s.ClientId, c.Name,
                s.Date, s.Site, s.EstimatedEquipmentCount,
                (SurveyStatusDto)s.Status, s.ConvertedJobOrderId, s.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<SurveyListItemDto>(rows, total, page, pageSize);
    }
}

public sealed class GetSurveyByIdHandler : IQueryHandler<GetSurveyByIdQuery, SurveyDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetSurveyByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<SurveyDetailDto?> Handle(GetSurveyByIdQuery q, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.Surveys.IgnoreQueryFilters().AsNoTracking() : _db.Surveys.AsNoTracking();
        var s = await query.FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (s is null) return null;
        var name = await _db.Clients.IgnoreQueryFilters().Where(c => c.Id == s.ClientId)
            .Select(c => c.Name).FirstAsync(ct);
        return new SurveyDetailDto(
            s.Id, s.SurveyNo, s.ClientId, name,
            s.Date, s.Site, s.GpsLatLng,
            s.EstimatedEquipmentCount, s.AccessNotes, s.SafetyNotes,
            s.Recommendation, s.SurveyorUserId,
            (SurveyStatusDto)s.Status, s.ConvertedJobOrderId,
            s.CreatedAtUtc, s.UpdatedAtUtc);
    }
}

public sealed class CreateSurveyHandler : ICommandHandler<CreateSurveyCommand, SurveyDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly SurveyNoGenerator _gen;
    public CreateSurveyHandler(AppDbContext db, ITenantContext tenant, IClock clock, SurveyNoGenerator gen)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; }

    public async Task<SurveyDetailDto> Handle(CreateSurveyCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator) && !_tenant.IsInRole(Roles.Inspector))
            throw new UnauthorizedAccessException("Coordinator, Inspector, or Manager required.");
        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == command.Body.ClientId, ct);
        if (!clientExists) throw new ArgumentException("Unknown client.");

        var no = await _gen.Next(ct);
        var s = new Survey(Guid.NewGuid(), no, command.Body.ClientId, command.Body.Date);
        s.UpdateDetails(command.Body.Site, null, 0, null, null, null, _tenant.UserId);
        s.CreatedAtUtc = _clock.UtcNow;
        s.CreatedById = _tenant.UserId;
        _db.Surveys.Add(s);
        await _db.SaveChangesAsync(ct);
        return (await new GetSurveyByIdHandler(_db, _tenant).Handle(new(s.Id), ct))!;
    }
}

public sealed class UpdateSurveyHandler : ICommandHandler<UpdateSurveyCommand, SurveyDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public UpdateSurveyHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<SurveyDetailDto> Handle(UpdateSurveyCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.Surveys.IgnoreQueryFilters() : _db.Surveys;
        var s = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Survey {command.Id} not found.");
        s.UpdateDetails(command.Body.Site, command.Body.GpsLatLng, command.Body.EstimatedEquipmentCount,
            command.Body.AccessNotes, command.Body.SafetyNotes, command.Body.Recommendation, command.Body.SurveyorUserId);
        s.UpdatedAtUtc = _clock.UtcNow;
        s.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetSurveyByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class SubmitSurveyHandler : ICommandHandler<SubmitSurveyCommand, SurveyDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public SubmitSurveyHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<SurveyDetailDto> Handle(SubmitSurveyCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager) ? _db.Surveys.IgnoreQueryFilters() : _db.Surveys;
        var s = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Survey {command.Id} not found.");
        s.Submit();
        s.UpdatedAtUtc = _clock.UtcNow;
        s.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return (await new GetSurveyByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}
