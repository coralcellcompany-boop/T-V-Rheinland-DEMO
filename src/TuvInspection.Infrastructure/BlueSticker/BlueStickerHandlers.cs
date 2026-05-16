using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.BlueSticker;

internal static class BlueStickerMapper
{
    public static BlueStickerReportDetailDto ToDetail(BlueStickerReport r) => new(
        r.Id, r.ReportNo, r.JobOrderId, r.EquipmentId, r.ClientId, r.TuvJobOrderNo,
        r.AramcoCategoryNo, r.OrgCode, r.RpoNo, r.CrmNo, r.DepartmentContractor,
        r.InspectionDate, r.InspectionTime, r.PreviousStickerNo, r.PreviousStickerIssuedBy,
        r.AreaOfInspection, (BlueStickerResultDto)(int)r.Result, r.EquipmentIdNo, r.Capacity,
        r.EquipmentLocation, r.Manufacturer, r.Model, r.EquipmentType, r.EquipmentSerialNo,
        r.NewStickerNo, r.StickerExpirationDate, r.Deficiencies, r.CorrectiveActionsTaken,
        r.ReceiverName, r.ReceiverBadgeNo, r.ReceiverTelephone, r.InspectorName, r.InspectorSapNo,
        r.InspectorTelephone, r.TechnicalReviewerName, r.ReceivedDate, r.ReviewedDate,
        r.ReceiverSignaturePng, r.InspectorSignaturePng, r.TechnicalReviewerSignaturePng,
        (BlueStickerReportStateDto)(int)r.State, r.CreatedAtUtc, r.UpdatedAtUtc,
        r.Transitions.OrderBy(t => t.AtUtc).Select(t => new BlueStickerTransitionDto(
            t.FromState.ToString(), t.ToState.ToString(), t.ActorUserId, t.ActorRole,
            t.Comments, t.AtUtc)).ToList());
}

public sealed class CreateBlueStickerReportsHandler
    : ICommandHandler<CreateBlueStickerReportsCommand, IReadOnlyList<BlueStickerReportDetailDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly BlueStickerReportNoGenerator _no;

    public CreateBlueStickerReportsHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        BlueStickerReportNoGenerator no)
    { _db = db; _tenant = tenant; _clock = clock; _no = no; }

    public async Task<IReadOnlyList<BlueStickerReportDetailDto>> Handle(
        CreateBlueStickerReportsCommand command, CancellationToken ct)
    {
        var b = command.Body;
        var jo = await _db.JobOrders.IgnoreQueryFilters()
            .FirstOrDefaultAsync(j => j.Id == b.JobOrderId, ct)
            ?? throw new KeyNotFoundException($"Job order {b.JobOrderId} not found.");

        var equipment = await _db.Equipment.IgnoreQueryFilters()
            .Where(e => e.ClientId == jo.ClientId
                        && e.AramcoCategory != null
                        && e.AramcoCategory != TuvInspection.Domain.Equipment.AramcoCategory.None)
            .ToListAsync(ct);
        if (equipment.Count == 0)
            throw new InvalidOperationException(
                "No Aramco-categorised equipment for this client — nothing to inspect for Blue Sticker.");

        var created = new List<BlueStickerReport>();
        foreach (var eq in equipment)
        {
            if (await _db.BlueStickerReports.IgnoreQueryFilters()
                    .AnyAsync(r => r.JobOrderId == jo.Id && r.EquipmentId == eq.Id, ct))
                continue; // idempotent

            var typeName = await _db.EquipmentTypes.IgnoreQueryFilters()
                .Where(t => t.Id == eq.EquipmentTypeId).Select(t => t.Name)
                .FirstOrDefaultAsync(ct);

            var report = new BlueStickerReport(Guid.NewGuid(), await _no.Next(ct),
                jo.Id, eq.Id, jo.ClientId, jo.JobOrderNo, eq.IdNo);
            report.SetAdminFields(b.OrgCode, b.RpoNo, b.CrmNo, b.DepartmentContractor,
                eq.AramcoCategory?.ToString());
            report.SetEquipmentSnapshot(eq.Swl, eq.Location, eq.Manufacturer, eq.Model,
                typeName, eq.SerialNo);

            var prev = await _db.Stickers.IgnoreQueryFilters()
                .Where(s => s.IssuedToEquipmentId == eq.Id)
                .OrderByDescending(s => s.IssuedAtUtc)
                .Select(s => new { s.StickerNo, s.AssignedToInspectorId })
                .FirstOrDefaultAsync(ct);
            if (prev is not null)
                report.SetPreviousSticker(prev.StickerNo, prev.AssignedToInspectorId);

            report.CreatedAtUtc = _clock.UtcNow;
            report.CreatedById = _tenant.UserId;
            _db.BlueStickerReports.Add(report);
            created.Add(report);
        }
        await _db.SaveChangesAsync(ct);
        return created.Select(BlueStickerMapper.ToDetail).ToList();
    }
}

public sealed class GetBlueStickerReportByIdHandler
    : IQueryHandler<GetBlueStickerReportByIdQuery, BlueStickerReportDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetBlueStickerReportByIdHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<BlueStickerReportDetailDto?> Handle(
        GetBlueStickerReportByIdQuery q, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(r => r.Transitions)
            : _db.BlueStickerReports.Include(r => r.Transitions);
        var r = await query.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        return r is null ? null : BlueStickerMapper.ToDetail(r);
    }
}

public sealed class ListBlueStickerReportsHandler
    : IQueryHandler<ListBlueStickerReportsQuery, PagedResult<BlueStickerReportListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListBlueStickerReportsHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<PagedResult<BlueStickerReportListItemDto>> Handle(
        ListBlueStickerReportsQuery q, CancellationToken ct)
    {
        IQueryable<BlueStickerReport> query = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().AsNoTracking()
            : _db.BlueStickerReports.AsNoTracking();
        if (q.JobOrderId is { } jid) query = query.Where(r => r.JobOrderId == jid);
        if (q.State is { } st) query = query.Where(r => (int)r.State == (int)st);
        if (!string.IsNullOrWhiteSpace(q.Search))
            query = query.Where(r => r.ReportNo.Contains(q.Search) ||
                                     r.EquipmentIdNo.Contains(q.Search));
        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);
        var items = await query.OrderByDescending(r => r.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(r => new BlueStickerReportListItemDto(
                r.Id, r.ReportNo, r.TuvJobOrderNo, r.EquipmentIdNo,
                (BlueStickerReportStateDto)(int)r.State, r.InspectionDate, r.CreatedAtUtc))
            .ToListAsync(ct);
        return new PagedResult<BlueStickerReportListItemDto>(items, total, page, pageSize);
    }
}

public sealed class UpdateBlueStickerInspectionHandler
    : ICommandHandler<UpdateBlueStickerInspectionCommand, BlueStickerReportDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateBlueStickerInspectionRequest> _validator;

    public UpdateBlueStickerInspectionHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateBlueStickerInspectionRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<BlueStickerReportDetailDto> Handle(
        UpdateBlueStickerInspectionCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        var reportQuery = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(x => x.Transitions)
            : _db.BlueStickerReports.Include(x => x.Transitions);
        var r = await reportQuery.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");
        var b = command.Body;
        r.UpdateInspectionData(b.AreaOfInspection, (BlueStickerResult)(int)b.Result,
            b.Deficiencies, b.CorrectiveActionsTaken, b.EquipmentLocation,
            b.ReceiverName, b.ReceiverBadgeNo, b.ReceiverTelephone, b.InspectorTelephone);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}
