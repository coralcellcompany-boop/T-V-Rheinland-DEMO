using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Assessments;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.Assessments;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Assessments;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Assessments;

public sealed class ListCandidatesHandler
    : IQueryHandler<ListCandidatesQuery, PagedResult<CandidateListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListCandidatesHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<CandidateListItemDto>> Handle(ListCandidatesQuery q, CancellationToken ct)
    {
        IQueryable<Candidate> query = _tenant.IsInRole(Roles.Manager)
            ? _db.Candidates.IgnoreQueryFilters().AsNoTracking()
            : _db.Candidates.AsNoTracking();

        if (q.ClientId is { } cid) query = query.Where(c => c.ClientId == cid);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(c =>
                EF.Functions.Like(c.FullName, $"%{s}%") ||
                EF.Functions.Like(c.IdentificationNumber, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderBy(c => c.FullName)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new { c, cl })
            .Select(x => new CandidateListItemDto(
                x.c.Id, x.c.ClientId, x.cl.Name,
                x.c.FullName, x.c.IdentificationNumber,
                x.c.Phone, x.c.Email, x.c.Nationality,
                x.c.IsActive, x.c.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CandidateListItemDto>(rows, total, page, pageSize);
    }
}

public sealed class GetCandidateByIdHandler
    : IQueryHandler<GetCandidateByIdQuery, CandidateDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetCandidateByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<CandidateDetailDto?> Handle(GetCandidateByIdQuery q, CancellationToken ct)
    {
        IQueryable<Candidate> query = _tenant.IsInRole(Roles.Manager)
            ? _db.Candidates.IgnoreQueryFilters().AsNoTracking()
            : _db.Candidates.AsNoTracking();

        var dto = await query
            .Where(c => c.Id == q.Id)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new CandidateDetailDto(
                c.Id, c.ClientId, cl.Name,
                c.FullName, c.IdentificationNumber,
                c.Phone, c.Email, c.EmployeeNo, c.Nationality, c.DateOfBirth, c.PhotoKey,
                c.IsActive, c.CreatedAtUtc, c.UpdatedAtUtc))
            .FirstOrDefaultAsync(ct);
        return dto;
    }
}

public sealed class CreateCandidateHandler
    : ICommandHandler<CreateCandidateCommand, CandidateDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateCandidateRequest> _validator;
    public CreateCandidateHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<CreateCandidateRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<CandidateDetailDto> Handle(CreateCandidateCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(command.Body.ClientId))
            throw new UnauthorizedAccessException("You can only create candidates under clients you are assigned to.");

        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == command.Body.ClientId, ct);
        if (!clientExists) throw new ArgumentException("Unknown client.");

        var dup = await _db.Candidates.IgnoreQueryFilters().AnyAsync(c =>
            c.ClientId == command.Body.ClientId &&
            c.IdentificationNumber == command.Body.IdentificationNumber, ct);
        if (dup) throw new ArgumentException(
            $"A candidate with ID {command.Body.IdentificationNumber} already exists for this client.");

        var c = new Candidate(Guid.NewGuid(), command.Body.ClientId,
            command.Body.FullName, command.Body.IdentificationNumber);
        c.UpdateProfile(command.Body.FullName, command.Body.IdentificationNumber,
            command.Body.Phone, command.Body.Email, command.Body.EmployeeNo,
            command.Body.Nationality, command.Body.DateOfBirth);
        c.CreatedAtUtc = _clock.UtcNow;
        c.CreatedById = _tenant.UserId;

        _db.Candidates.Add(c);
        await _db.SaveChangesAsync(ct);

        return (await new GetCandidateByIdHandler(_db, _tenant).Handle(new(c.Id), ct))!;
    }
}

public sealed class UpdateCandidateHandler
    : ICommandHandler<UpdateCandidateCommand, CandidateDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateCandidateRequest> _validator;
    public UpdateCandidateHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateCandidateRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<CandidateDetailDto> Handle(UpdateCandidateCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.Candidates.IgnoreQueryFilters()
            : _db.Candidates;
        var c = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Candidate {command.Id} not found.");

        c.UpdateProfile(command.Body.FullName, command.Body.IdentificationNumber,
            command.Body.Phone, command.Body.Email, command.Body.EmployeeNo,
            command.Body.Nationality, command.Body.DateOfBirth);
        if (command.Body.IsActive) c.Reactivate(); else c.Deactivate();
        c.UpdatedAtUtc = _clock.UtcNow;
        c.UpdatedById = _tenant.UserId;

        await _db.SaveChangesAsync(ct);
        return (await new GetCandidateByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}
