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

public sealed class ListCompetencyCardsHandler
    : IQueryHandler<ListCompetencyCardsQuery, PagedResult<CompetencyCardListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListCompetencyCardsHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<CompetencyCardListItemDto>> Handle(
        ListCompetencyCardsQuery q, CancellationToken ct)
    {
        IQueryable<CompetencyCard> query = _db.CompetencyCards.AsNoTracking();
        if (!_tenant.IsInRole(Roles.Manager))
            query = query.Where(c => _tenant.AssignedClientIds.Contains(c.ClientId));

        if (q.ClientId is { } clid) query = query.Where(c => c.ClientId == clid);
        if (q.CandidateId is { } cid) query = query.Where(c => c.CandidateId == cid);
        if (q.State is { } st) query = query.Where(c => c.State == (CompetencyCardState)(int)st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(c => EF.Functions.Like(c.CardNo, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Assessments.IgnoreQueryFilters(), c => c.AssessmentId, a => a.Id, (c, a) => new { c, a })
            .Join(_db.Candidates.IgnoreQueryFilters(), x => x.c.CandidateId, ca => ca.Id, (x, ca) => new { x.c, x.a, ca })
            .Join(_db.Clients.IgnoreQueryFilters(), x => x.c.ClientId, cl => cl.Id, (x, cl) => new CompetencyCardListItemDto(
                x.c.Id, x.c.CardNo,
                x.c.AssessmentId, x.a.AssessmentNo,
                x.c.CandidateId, x.ca.FullName,
                x.c.ClientId, cl.Name,
                (CompetencyCategoryDto)x.c.Category,
                x.c.IssuedOn, x.c.ValidUntil,
                (CompetencyCardStateDto)x.c.State))
            .ToListAsync(ct);

        return new PagedResult<CompetencyCardListItemDto>(rows, total, page, pageSize);
    }
}

public sealed class GetCompetencyCardPublicViewHandler
    : IQueryHandler<GetCompetencyCardPublicViewQuery, CompetencyCardPublicViewDto?>
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    public GetCompetencyCardPublicViewHandler(AppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async Task<CompetencyCardPublicViewDto?> Handle(
        GetCompetencyCardPublicViewQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.CardNo)) return null;
        var no = q.CardNo.Trim().ToUpperInvariant();

        var card = await _db.CompetencyCards.IgnoreQueryFilters().AsNoTracking()
            .FirstOrDefaultAsync(c => c.CardNo == no, ct);
        if (card is null) return null;

        var candidate = await _db.Candidates.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == card.CandidateId)
            .Select(c => new { c.FullName, c.IdentificationNumber }).FirstOrDefaultAsync(ct);
        var clientName = await _db.Clients.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == card.ClientId).Select(c => c.Name).FirstOrDefaultAsync(ct);

        var nameMasked = MaskName(candidate?.FullName ?? "");
        var idMasked = MaskId(candidate?.IdentificationNumber ?? "");

        var isValid = card.State == CompetencyCardState.Issued
            && card.ValidUntil.HasValue
            && card.ValidUntil.Value >= DateOnly.FromDateTime(_clock.UtcNow);

        return new CompetencyCardPublicViewDto(
            card.CardNo,
            (CompetencyCategoryDto)card.Category,
            nameMasked, idMasked,
            clientName,
            card.IssuedOn, card.ValidUntil,
            isValid,
            (CompetencyCardStateDto)card.State);
    }

    private static string MaskName(string full)
    {
        if (string.IsNullOrWhiteSpace(full)) return "—";
        var parts = full.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1) return parts[0];
        return $"{parts[0]} {parts[^1][0]}.";
    }

    private static string MaskId(string id)
    {
        if (string.IsNullOrWhiteSpace(id)) return "—";
        var len = id.Length;
        return len <= 4 ? id : new string('•', len - 4) + id[^4..];
    }
}
