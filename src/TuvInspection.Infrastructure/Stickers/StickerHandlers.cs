using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.Stickers;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Stickers;
using TuvInspection.Domain.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.Stickers;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Stickers;

public sealed class ListStickersHandler : IQueryHandler<ListStickersQuery, PagedResult<StickerListItemDto>>
{
    private readonly AppDbContext _db;
    public ListStickersHandler(AppDbContext db) => _db = db;

    public async Task<PagedResult<StickerListItemDto>> Handle(ListStickersQuery q, CancellationToken ct)
    {
        IQueryable<Sticker> query = _db.Stickers.AsNoTracking();
        if (q.State is { } st) query = query.Where(s => s.State == (StickerState)(int)st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToUpperInvariant();
            query = query.Where(x => EF.Functions.Like(x.StickerNo, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        // Project with a left-join to clients/equipment/cert (ignore tenant filters — admin view).
        var rows = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.StickerNo, s.State, s.ClientId, s.IssuedToCertificateId,
                s.IssuedToEquipmentId, s.ValidUntil, s.CreatedAtUtc,
                ClientName = _db.Clients.IgnoreQueryFilters()
                    .Where(c => c.Id == s.ClientId).Select(c => c.Name).FirstOrDefault(),
                CertificateNo = _db.Certificates.IgnoreQueryFilters()
                    .Where(c => c.Id == s.IssuedToCertificateId).Select(c => c.CertificateNo).FirstOrDefault(),
                EquipmentIdNo = _db.Equipment.IgnoreQueryFilters()
                    .Where(e => e.Id == s.IssuedToEquipmentId).Select(e => e.IdNo).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new StickerListItemDto(
            r.Id, r.StickerNo, (StickerStateDto)r.State,
            r.ClientId, r.ClientName,
            r.IssuedToCertificateId, r.CertificateNo,
            r.IssuedToEquipmentId, r.EquipmentIdNo,
            r.ValidUntil, r.CreatedAtUtc)).ToList();

        return new PagedResult<StickerListItemDto>(items, total, page, pageSize);
    }
}

public sealed class GetStickerStockSummaryHandler
    : IQueryHandler<GetStickerStockSummaryQuery, StickerStockSummaryDto>
{
    private readonly AppDbContext _db;
    public GetStickerStockSummaryHandler(AppDbContext db) => _db = db;

    public async Task<StickerStockSummaryDto> Handle(GetStickerStockSummaryQuery q, CancellationToken ct)
    {
        var counts = await _db.Stickers.AsNoTracking()
            .GroupBy(s => s.State)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Get(StickerState s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        return new StickerStockSummaryDto(
            Get(StickerState.Unallocated),
            Get(StickerState.Issued),
            Get(StickerState.Voided),
            Get(StickerState.Expired));
    }
}

public sealed class GetStickerPublicViewHandler
    : IQueryHandler<GetStickerPublicViewQuery, StickerPublicViewDto?>
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;
    public GetStickerPublicViewHandler(AppDbContext db, IClock clock) { _db = db; _clock = clock; }

    public async Task<StickerPublicViewDto?> Handle(GetStickerPublicViewQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.StickerNo)) return null;

        var s = await _db.Stickers.AsNoTracking()
            .FirstOrDefaultAsync(x => x.StickerNo == q.StickerNo.Trim().ToUpper(), ct);
        if (s is null) return null;

        string? clientName = s.ClientId is null ? null
            : await _db.Clients.IgnoreQueryFilters()
                .Where(c => c.Id == s.ClientId).Select(c => c.Name).FirstOrDefaultAsync(ct);

        string? certNo = s.IssuedToCertificateId is null ? null
            : await _db.Certificates.IgnoreQueryFilters()
                .Where(c => c.Id == s.IssuedToCertificateId).Select(c => c.CertificateNo).FirstOrDefaultAsync(ct);

        string? equipIdNo = null;
        string? equipTypeName = null;
        AramcoCategory? aramcoCat = null;
        if (s.IssuedToEquipmentId is { } eqId)
        {
            var eq = await _db.Equipment.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.Id == eqId)
                .Join(_db.EquipmentTypes, e => e.EquipmentTypeId, t => t.Id,
                    (e, t) => new { e.IdNo, e.AramcoCategory, TypeName = t.Name })
                .FirstOrDefaultAsync(ct);
            equipIdNo = eq?.IdNo;
            equipTypeName = eq?.TypeName;
            aramcoCat = eq?.AramcoCategory;
        }

        // Mask equipment ID — show last 6 chars only.
        var maskedIdNo = string.IsNullOrEmpty(equipIdNo)
            ? null
            : equipIdNo.Length <= 6 ? equipIdNo : "…" + equipIdNo[^6..];

        var isValid = s.State == StickerState.Issued
            && s.ValidUntil.HasValue
            && s.ValidUntil.Value >= DateOnly.FromDateTime(_clock.UtcNow);

        return new StickerPublicViewDto(
            s.StickerNo,
            (StickerStateDto)s.State,
            aramcoCat?.ToString(),
            equipTypeName,
            maskedIdNo,
            clientName,
            s.ValidUntil,
            isValid,
            certNo,
            s.IssuedAtUtc);
    }
}

public sealed class ProcureStickerStockHandler : ICommandHandler<ProcureStickerStockCommand, int>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly StickerNumberGenerator _gen;

    public ProcureStickerStockHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        StickerNumberGenerator gen)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; }

    public async Task<int> Handle(ProcureStickerStockCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may procure sticker stock.");
        if (command.Count is < 1 or > 1000)
            throw new ArgumentException("Count must be between 1 and 1000.");

        var nos = await _gen.NextBatch(command.Count, ct);
        foreach (var n in nos)
        {
            var sticker = new Sticker(Guid.NewGuid(), n)
            {
                CreatedAtUtc = _clock.UtcNow,
                CreatedById = _tenant.UserId,
            };
            _db.Stickers.Add(sticker);
        }
        await _db.SaveChangesAsync(ct);
        return nos.Count;
    }
}

public sealed class VoidStickerHandler : ICommandHandler<VoidStickerCommand, StickerListItemDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    public VoidStickerHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<StickerListItemDto> Handle(VoidStickerCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may void stickers.");

        var s = await _db.Stickers.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Sticker {command.Id} not found.");
        s.Void(command.Reason, _clock.UtcNow);
        s.UpdatedAtUtc = _clock.UtcNow;
        s.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);

        return new StickerListItemDto(
            s.Id, s.StickerNo, (StickerStateDto)s.State,
            s.ClientId, null,
            s.IssuedToCertificateId, null,
            s.IssuedToEquipmentId, null,
            s.ValidUntil, s.CreatedAtUtc);
    }
}
