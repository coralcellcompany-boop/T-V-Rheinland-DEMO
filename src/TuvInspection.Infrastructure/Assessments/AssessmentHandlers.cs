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

internal static class AssessmentMapper
{
    public static AssessmentTransitionDto ToDto(this AssessmentTransition t) =>
        new(t.Id,
            (AssessmentStateDto)t.FromState,
            (AssessmentStateDto)t.ToState,
            t.ActorUserId, t.ActorRole, t.Comments, t.AtUtc);
}

public sealed class ListAssessmentsHandler
    : IQueryHandler<ListAssessmentsQuery, PagedResult<AssessmentListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListAssessmentsHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<AssessmentListItemDto>> Handle(ListAssessmentsQuery q, CancellationToken ct)
    {
        IQueryable<Assessment> query = _tenant.IsInRole(Roles.Manager)
            ? _db.Assessments.IgnoreQueryFilters().AsNoTracking()
            : _db.Assessments.AsNoTracking();

        if (q.CandidateId is { } cid) query = query.Where(a => a.CandidateId == cid);
        if (q.ClientId is { } clid) query = query.Where(a => a.ClientId == clid);
        if (q.State is { } st) query = query.Where(a => a.State == (AssessmentState)(int)st);
        if (q.Category is { } cat) query = query.Where(a => a.Category == (CompetencyCategory)(int)cat);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(a => EF.Functions.Like(a.AssessmentNo, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(a => a.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Candidates.IgnoreQueryFilters(), a => a.CandidateId, c => c.Id, (a, c) => new { a, c })
            .Join(_db.Clients.IgnoreQueryFilters(), x => x.a.ClientId, cl => cl.Id, (x, cl) => new AssessmentListItemDto(
                x.a.Id, x.a.AssessmentNo, x.a.CandidateId, x.c.FullName,
                x.a.ClientId, cl.Name,
                (CompetencyCategoryDto)x.a.Category,
                x.a.AssessmentDate, x.a.NextAssessmentDate,
                (AssessmentResultDto)x.a.Result,
                (AssessmentStateDto)x.a.State,
                x.a.IssuedCardNo, x.a.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<AssessmentListItemDto>(rows, total, page, pageSize);
    }
}

public sealed class GetAssessmentByIdHandler
    : IQueryHandler<GetAssessmentByIdQuery, AssessmentDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetAssessmentByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<AssessmentDetailDto?> Handle(GetAssessmentByIdQuery q, CancellationToken ct)
    {
        IQueryable<Assessment> baseQ = _tenant.IsInRole(Roles.Manager)
            ? _db.Assessments.IgnoreQueryFilters().Include(a => a.Transitions)
            : _db.Assessments.Include(a => a.Transitions);

        var a = await baseQ.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (a is null) return null;

        var c = await _db.Candidates.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == a.CandidateId)
            .Select(x => new { x.FullName, x.IdentificationNumber }).FirstAsync(ct);
        var clientName = await _db.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(x => x.Id == a.ClientId).Select(x => x.Name).FirstAsync(ct);

        return new AssessmentDetailDto(
            a.Id, a.AssessmentNo,
            a.CandidateId, c.FullName, c.IdentificationNumber,
            a.ClientId, clientName,
            (CompetencyCategoryDto)a.Category,
            a.AssessmentDate, a.NextAssessmentDate, a.Location,
            a.TheoreticalScore, a.PracticalScore,
            (AssessmentResultDto)a.Result, a.Comments,
            (AssessmentStateDto)a.State,
            a.IssuedCardId, a.IssuedCardNo,
            a.CreatedAtUtc, a.UpdatedAtUtc,
            a.Transitions.OrderBy(t => t.AtUtc).Select(t => t.ToDto()).ToList());
    }
}

public sealed class CreateAssessmentHandler
    : ICommandHandler<CreateAssessmentCommand, AssessmentDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateAssessmentRequest> _validator;
    private readonly AssessmentNoGenerator _no;
    public CreateAssessmentHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<CreateAssessmentRequest> validator, AssessmentNoGenerator no)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; _no = no; }

    public async Task<AssessmentDetailDto> Handle(CreateAssessmentCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var candidate = await _db.Candidates.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.Id == command.Body.CandidateId, ct)
            ?? throw new ArgumentException("Candidate not found.");

        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(candidate.ClientId))
            throw new UnauthorizedAccessException("You can only create assessments under clients you are assigned to.");

        var assessmentNo = await _no.Next(ct);
        var a = new Assessment(Guid.NewGuid(), assessmentNo,
            candidate.Id, candidate.ClientId,
            (CompetencyCategory)(int)command.Body.Category,
            command.Body.AssessmentDate);
        a.UpdateScores(null, null, AssessmentResult.NotSet, null, null, command.Body.Location);
        a.CreatedAtUtc = _clock.UtcNow;
        a.CreatedById = _tenant.UserId;

        _db.Assessments.Add(a);
        await _db.SaveChangesAsync(ct);

        return (await new GetAssessmentByIdHandler(_db, _tenant).Handle(new(a.Id), ct))!;
    }
}

public sealed class UpdateAssessmentHandler
    : ICommandHandler<UpdateAssessmentCommand, AssessmentDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateAssessmentRequest> _validator;
    public UpdateAssessmentHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateAssessmentRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<AssessmentDetailDto> Handle(UpdateAssessmentCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.Assessments.IgnoreQueryFilters()
            : _db.Assessments;
        var a = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Assessment {command.Id} not found.");

        a.UpdateScores(command.Body.TheoreticalScore, command.Body.PracticalScore,
            (AssessmentResult)(int)command.Body.Result, command.Body.Comments,
            command.Body.NextAssessmentDate, command.Body.Location);
        a.UpdatedAtUtc = _clock.UtcNow;
        a.UpdatedById = _tenant.UserId;

        await _db.SaveChangesAsync(ct);
        return (await new GetAssessmentByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class FireAssessmentTriggerHandler
    : ICommandHandler<FireAssessmentTriggerCommand, AssessmentDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly CompetencyCardNoGenerator _cardNo;

    public FireAssessmentTriggerHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        CompetencyCardNoGenerator cardNo)
    { _db = db; _tenant = tenant; _clock = clock; _cardNo = cardNo; }

    public async Task<AssessmentDetailDto> Handle(FireAssessmentTriggerCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.Assessments.IgnoreQueryFilters().Include(a => a.Transitions)
            : _db.Assessments.Include(a => a.Transitions);

        var a = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Assessment {command.Id} not found.");

        var trigger = (AssessmentTrigger)(int)command.Trigger;
        var sm = new AssessmentStateMachine(a, _tenant, _clock);
        if (!sm.CanFire(trigger))
            throw new InvalidOperationException(
                $"Cannot {trigger} an assessment currently in state {a.State}.");

        sm.Fire(trigger, command.Comments);

        // Auto-issue competency card on Approved (only when result = Pass).
        if (a.State == AssessmentState.Approved && a.Result == AssessmentResult.Pass && a.IssuedCardId is null)
        {
            var cardNo = await _cardNo.Next(ct);
            var validUntil = a.NextAssessmentDate
                ?? DateOnly.FromDateTime(_clock.UtcNow).AddYears(1);
            var card = new CompetencyCard(
                Guid.NewGuid(), cardNo,
                a.Id, a.CandidateId, a.ClientId,
                a.Category,
                DateOnly.FromDateTime(_clock.UtcNow),
                validUntil)
            {
                CreatedAtUtc = _clock.UtcNow,
                CreatedById = _tenant.UserId,
            };
            _db.CompetencyCards.Add(card);
            a.LinkCard(card.Id, card.CardNo);
        }

        await _db.SaveChangesAsync(ct);
        return (await new GetAssessmentByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}
