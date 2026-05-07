using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
        if (q.Color is { } c) query = query.Where(s => s.Color == (StickerColor)(int)c);
        if (!string.IsNullOrWhiteSpace(q.AssignedToInspectorId))
            query = query.Where(s => s.AssignedToInspectorId == q.AssignedToInspectorId);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim().ToUpperInvariant();
            query = query.Where(x => EF.Functions.Like(x.StickerNo, $"%{s}%"));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var rows = await query
            .OrderByDescending(s => s.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(s => new
            {
                s.Id, s.StickerNo, s.State, s.Color, s.ClientId,
                s.AssignedToInspectorId,
                s.IssuedToCertificateId, s.IssuedToEquipmentId,
                s.ValidUntil, s.CreatedAtUtc,
                AssignedToInspectorName = _db.Users
                    .Where(u => u.Id == s.AssignedToInspectorId)
                    .Select(u => u.FullName ?? u.UserName ?? u.Email)
                    .FirstOrDefault(),
                ClientName = _db.Clients.IgnoreQueryFilters()
                    .Where(c => c.Id == s.ClientId).Select(c => c.Name).FirstOrDefault(),
                CertificateNo = _db.Certificates.IgnoreQueryFilters()
                    .Where(c => c.Id == s.IssuedToCertificateId).Select(c => c.CertificateNo).FirstOrDefault(),
                EquipmentIdNo = _db.Equipment.IgnoreQueryFilters()
                    .Where(e => e.Id == s.IssuedToEquipmentId).Select(e => e.IdNo).FirstOrDefault(),
            })
            .ToListAsync(ct);

        var items = rows.Select(r => new StickerListItemDto(
            r.Id, r.StickerNo, (StickerStateDto)r.State, (StickerColorDto)r.Color,
            r.AssignedToInspectorId, r.AssignedToInspectorName,
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
    private readonly IConfiguration _config;
    public GetStickerStockSummaryHandler(AppDbContext db, IConfiguration config)
    { _db = db; _config = config; }

    public async Task<StickerStockSummaryDto> Handle(GetStickerStockSummaryQuery q, CancellationToken ct)
    {
        var counts = await _db.Stickers.AsNoTracking()
            .GroupBy(s => s.State)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToListAsync(ct);
        int Get(StickerState s) => counts.FirstOrDefault(c => c.Key == s)?.Count ?? 0;
        var unallocated = Get(StickerState.Unallocated);

        // Threshold is configurable so a contract that ramps up sticker volumes can raise it
        // without a code change. Default 50 — small enough that the team has time to procure
        // a fresh batch before approvals start failing.
        var threshold = _config.GetValue<int?>("Stickers:LowStockThreshold") ?? 50;

        return new StickerStockSummaryDto(
            unallocated,
            Get(StickerState.Issued),
            Get(StickerState.Voided),
            Get(StickerState.Expired),
            threshold,
            unallocated <= threshold);
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

        string? certNo = null;
        DateOnly? inspectionDate = null;
        string? inspectorName = null;
        string? inspectorSap = null;
        if (s.IssuedToCertificateId is { } certId)
        {
            var cert = await _db.Certificates.IgnoreQueryFilters().AsNoTracking()
                .Where(c => c.Id == certId)
                .Select(c => new { c.CertificateNo, c.InspectionDate, c.CreatedById })
                .FirstOrDefaultAsync(ct);
            certNo = cert?.CertificateNo;
            inspectionDate = cert?.InspectionDate;
            if (cert?.CreatedById is { } uid)
            {
                var user = await _db.Users.AsNoTracking()
                    .Where(u => u.Id == uid)
                    .Select(u => new { Name = u.FullName ?? u.UserName ?? u.Email, Sap = u.SapNo })
                    .FirstOrDefaultAsync(ct);
                inspectorName = user?.Name;
                inspectorSap = user?.Sap;
            }
        }

        string? equipIdNo = null;
        string? equipSerialNo = null;
        string? equipSwl = null;
        string? equipTypeName = null;
        AramcoCategory? aramcoCat = null;
        if (s.IssuedToEquipmentId is { } eqId)
        {
            var eq = await _db.Equipment.IgnoreQueryFilters().AsNoTracking()
                .Where(e => e.Id == eqId)
                .Join(_db.EquipmentTypes, e => e.EquipmentTypeId, t => t.Id,
                    (e, t) => new { e.IdNo, e.SerialNo, e.Swl, e.AramcoCategory, TypeName = t.Name })
                .FirstOrDefaultAsync(ct);
            equipIdNo = eq?.IdNo;
            equipSerialNo = eq?.SerialNo;
            equipSwl = eq?.Swl;
            equipTypeName = eq?.TypeName;
            aramcoCat = eq?.AramcoCategory;
        }

        // Mask equipment ID + serial — show last 6 chars only (no PII over public scan).
        static string? Mask(string? v) => string.IsNullOrEmpty(v)
            ? null : (v.Length <= 6 ? v : "…" + v[^6..]);

        var isValid = s.State == StickerState.Issued
            && s.ValidUntil.HasValue
            && s.ValidUntil.Value >= DateOnly.FromDateTime(_clock.UtcNow);

        return new StickerPublicViewDto(
            s.StickerNo,
            (StickerStateDto)s.State,
            aramcoCat?.ToString(),
            equipTypeName,
            Mask(equipIdNo),
            Mask(equipSerialNo),
            equipSwl,
            clientName,
            inspectionDate,
            s.ValidUntil,
            isValid,
            certNo,
            inspectorName,
            inspectorSap,
            s.IssuedAtUtc);
    }
}

public sealed class ProcureStickerStockHandler : ICommandHandler<ProcureStickerStockCommand, int>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly StickerNumberGenerator _gen;
    private readonly IValidator<ProcureStockRequest> _validator;

    public ProcureStickerStockHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        StickerNumberGenerator gen, IValidator<ProcureStockRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _gen = gen; _validator = validator; }

    public async Task<int> Handle(ProcureStickerStockCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may procure sticker stock.");

        await _validator.ValidateAndThrowAsync(new ProcureStockRequest(command.Count, command.Color), ct);

        var color = (StickerColor)(int)command.Color;
        var nos = await _gen.NextBatch(command.Count, ct);
        foreach (var n in nos)
        {
            var sticker = new Sticker(Guid.NewGuid(), n, color)
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
    private readonly IValidator<VoidStickerRequest> _validator;
    public VoidStickerHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<VoidStickerRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<StickerListItemDto> Handle(VoidStickerCommand command, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may void stickers.");

        await _validator.ValidateAndThrowAsync(new VoidStickerRequest(command.Reason), ct);

        var s = await _db.Stickers.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Sticker {command.Id} not found.");
        s.Void(command.Reason, _clock.UtcNow);
        s.UpdatedAtUtc = _clock.UtcNow;
        s.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);

        return new StickerListItemDto(
            s.Id, s.StickerNo, (StickerStateDto)s.State, (StickerColorDto)s.Color,
            s.AssignedToInspectorId, null,
            s.ClientId, null,
            s.IssuedToCertificateId, null,
            s.IssuedToEquipmentId, null,
            s.ValidUntil, s.CreatedAtUtc);
    }
}

public sealed class AssignStickersToInspectorHandler
    : ICommandHandler<AssignStickersToInspectorCommand, int>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<AssignStickersRequest> _validator;
    public AssignStickersToInspectorHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<AssignStickersRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<int> Handle(AssignStickersToInspectorCommand c, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.IsInRole(Roles.Coordinator))
            throw new UnauthorizedAccessException("Only Manager or Coordinator may assign stickers.");

        await _validator.ValidateAndThrowAsync(
            new AssignStickersRequest(c.InspectorUserId, c.Color, c.Count), ct);

        var color = (StickerColor)(int)c.Color;
        var pool = await _db.Stickers
            .Where(s => s.State == StickerState.Unallocated
                && s.Color == color
                && s.AssignedToInspectorId == null)
            .OrderBy(s => s.CreatedAtUtc)
            .Take(c.Count)
            .ToListAsync(ct);

        var taken = pool.Count;
        if (taken == 0) return 0;

        foreach (var s in pool)
        {
            s.AssignToInspector(c.InspectorUserId, c.FromRequestId, _clock.UtcNow);
            s.UpdatedAtUtc = _clock.UtcNow;
            s.UpdatedById = _tenant.UserId;
        }
        await _db.SaveChangesAsync(ct);
        return taken;
    }
}
