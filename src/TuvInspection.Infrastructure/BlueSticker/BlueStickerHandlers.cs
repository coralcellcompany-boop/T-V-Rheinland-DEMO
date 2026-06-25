using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.Stickers;
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
            t.Comments, t.AtUtc)).ToList(),
        InspectionChecklistNumber: SaicChecklistMap.Resolve(r.AramcoCategoryNo, r.EquipmentType)
            ?? AramcoCategoryShortCodeFallback(r.AramcoCategoryNo));

    /// <summary>Category-level fallback: maps "CR##" back to the SAIC-U-70## checklist when the
    /// specific equipment type isn't in <see cref="SaicChecklistMap"/>. Reverse of
    /// <c>AramcoCategoryInfo.ChecklistNumber</c> — we store the short code on the report so the
    /// lookup is purely string-driven here.</summary>
    private static string? AramcoCategoryShortCodeFallback(string? shortCode) => shortCode switch
    {
        "CR01" => "SAIC-U-7007",
        "CR02" => "SAIC-U-7005",
        "CR03" => "SAIC-U-7013",
        "CR04" => "SAIC-U-7018",
        "CR05" => "SAIC-U-7012",
        "CR06" => "SAIC-U-7004",
        "CR07" => "SAIC-U-7002",
        "CR08" => "SAIC-U-7016",
        "CR09" => "SAIC-U-7013",
        "CR10" => "SAIC-U-7017",
        "CR11" => "SAIC-U-7008",
        "CR12" => "SAIC-U-7010",
        "CR13" => "SAIC-U-7008",
        "CR14" => "SAIC-U-7003",
        _ => null,
    };
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

        var equipQuery = _db.Equipment.IgnoreQueryFilters()
            .Where(e => e.ClientId == jo.ClientId
                        && e.AramcoCategory != null
                        && e.AramcoCategory != AramcoCategory.None);
        if (b.EquipmentIds is { Count: > 0 })
        {
            var ids = b.EquipmentIds.ToHashSet();
            equipQuery = equipQuery.Where(e => ids.Contains(e.Id));
        }
        var equipment = await equipQuery.ToListAsync(ct);
        if (equipment.Count == 0)
            throw new InvalidOperationException(b.EquipmentIds is { Count: > 0 }
                ? "None of the selected equipment match this client + Aramco-categorisation."
                : "No Aramco-categorised equipment for this client — nothing to inspect for Blue Sticker.");

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
                eq.AramcoCategory?.ShortCode());
            report.SetEquipmentSnapshot(eq.Swl, eq.Location, eq.Manufacturer, eq.Model,
                typeName, eq.SerialNo);

            var prev = await _db.Stickers.IgnoreQueryFilters()
                .Where(s => s.IssuedToEquipmentId == eq.Id)
                .OrderByDescending(s => s.IssuedAtUtc)
                .Select(s => new { s.StickerNo, s.AssignedToInspectorId })
                .FirstOrDefaultAsync(ct);
            if (prev is not null)
            {
                string? prevIssuedBy = null;
                if (!string.IsNullOrWhiteSpace(prev.AssignedToInspectorId))
                {
                    var inspId = prev.AssignedToInspectorId;
                    prevIssuedBy = await _db.Users.AsNoTracking()
                        .Where(u => u.Id == inspId)
                        .Select(u => u.FullName ?? u.UserName ?? u.Email)
                        .FirstOrDefaultAsync(ct) ?? prev.AssignedToInspectorId;
                }
                report.SetPreviousSticker(prev.StickerNo, prevIssuedBy);
            }

            report.CreatedAtUtc = _clock.UtcNow;
            report.CreatedById = _tenant.UserId;
            _db.BlueStickerReports.Add(report);
            created.Add(report);
        }
        await _db.SaveChangesAsync(ct);
        return created.Select(BlueStickerMapper.ToDetail).ToList();
    }
}

public sealed class GetBlueStickerReportByStickerNoHandler
    : IQueryHandler<GetBlueStickerReportByStickerNoQuery, BlueStickerReportDetailDto?>
{
    private readonly AppDbContext _db;
    public GetBlueStickerReportByStickerNoHandler(AppDbContext db) => _db = db;

    public async Task<BlueStickerReportDetailDto?> Handle(
        GetBlueStickerReportByStickerNoQuery q, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(q.StickerNo)) return null;
        var no = q.StickerNo.Trim().ToUpperInvariant();
        // IgnoreQueryFilters because this is an anonymous public endpoint — the QR on the
        // physical sticker has no tenant context. A sticker is only linked at Approve, so any
        // report bound to it is at least Approved. Voided reports are excluded so a withdrawn
        // sticker doesn't continue to serve its old certificate. Most recently created wins
        // for the "sticker re-issued after a re-inspection" case.
        var r = await _db.BlueStickerReports.IgnoreQueryFilters()
            .Include(x => x.Transitions)
            .Where(x => x.NewStickerNo == no
                && x.State != BlueStickerReportState.Voided)
            .OrderByDescending(x => x.CreatedAtUtc)
            .FirstOrDefaultAsync(ct);
        return r is null ? null : BlueStickerMapper.ToDetail(r);
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

public sealed class UpdateBlueStickerAdminHandler
    : ICommandHandler<UpdateBlueStickerAdminCommand, BlueStickerReportDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public UpdateBlueStickerAdminHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<BlueStickerReportDetailDto> Handle(
        UpdateBlueStickerAdminCommand command, CancellationToken ct)
    {
        var reportQuery = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(x => x.Transitions)
            : _db.BlueStickerReports.Include(x => x.Transitions);
        var r = await reportQuery.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");
        var b = command.Body;
        r.SetAdminFields(b.OrgCode, b.RpoNo, b.CrmNo, b.DepartmentContractor, b.AramcoCategoryNo);
        r.SetPreviousSticker(b.PreviousStickerNo, b.PreviousStickerIssuedBy);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
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
        r.UpdateEquipmentDetails(b.AramcoCategoryNo, b.Manufacturer, b.Model,
            b.EquipmentType, b.EquipmentSerialNo, b.Capacity);
        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}

public sealed class FireBlueStickerTriggerHandler
    : ICommandHandler<FireBlueStickerTriggerCommand, BlueStickerReportDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public FireBlueStickerTriggerHandler(AppDbContext db, ITenantContext tenant, IClock clock)
    { _db = db; _tenant = tenant; _clock = clock; }

    public async Task<BlueStickerReportDetailDto> Handle(
        FireBlueStickerTriggerCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(x => x.Transitions)
            : _db.BlueStickerReports.Include(x => x.Transitions);
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");

        var trigger = (BlueStickerReportTrigger)(int)command.Trigger;

        if (trigger == BlueStickerReportTrigger.SubmitForReview)
        {
            var missing = new List<string>();
            if (string.IsNullOrWhiteSpace(r.AreaOfInspection)) missing.Add("Area of Inspection");
            if (r.Result == BlueStickerResult.NotSet) missing.Add("Inspection Result");
            if (string.IsNullOrWhiteSpace(r.ReceiverName)) missing.Add("Receiver Name");
            if (string.IsNullOrWhiteSpace(r.ReceiverBadgeNo)) missing.Add("Receiver Badge No.");
            if (missing.Count > 0)
                throw new InvalidOperationException(
                    "Cannot submit — fill in: " + string.Join(", ", missing) + ".");

            var insp = _tenant.UserId is null ? null : await _db.Users.AsNoTracking()
                .Where(u => u.Id == _tenant.UserId)
                .Select(u => new { Name = u.FullName ?? u.UserName ?? u.Email, u.SapNo, u.SignaturePng })
                .FirstOrDefaultAsync(ct);
            // Auto-pull the inspector's stored signature when the caller didn't supply one.
            // Forces inspectors to set their signature on their profile before they can submit.
            var sig = command.Body?.InspectorSignaturePng ?? insp?.SignaturePng;
            if (string.IsNullOrWhiteSpace(sig))
                throw new InvalidOperationException(
                    "Inspector signature missing — set your signature on your profile first.");
            r.SetInspectorSnapshot(insp?.Name, insp?.SapNo, sig);
        }

        if (trigger == BlueStickerReportTrigger.StartInspection)
        {
            var nowUtc = _clock.UtcNow; // store UTC date/time of start
            r.StampInspectionStart(DateOnly.FromDateTime(nowUtc), TimeOnly.FromDateTime(nowUtc));
        }

        if (trigger == BlueStickerReportTrigger.Approve && r.Result == BlueStickerResult.Fail)
            throw new InvalidOperationException(
                "Cannot approve a Failed Blue Sticker inspection. Re-inspect or void.");

        var sm = new BlueStickerReportStateMachine(r, _tenant, _clock);
        if (!sm.CanFire(trigger))
            throw new InvalidOperationException(
                $"Cannot {trigger} a report currently in state {r.State}.");
        sm.Fire(trigger, command.Body?.Comments);

        if (r.State == BlueStickerReportState.Approved)
        {
            var reviewerRow = _tenant.UserId is null ? null : await _db.Users.AsNoTracking()
                .Where(u => u.Id == _tenant.UserId)
                .Select(u => new { Name = u.FullName ?? u.UserName ?? u.Email, u.SignaturePng })
                .FirstOrDefaultAsync(ct);
            var reviewer = reviewerRow?.Name ?? "Reviewer";
            var reviewerSig = command.Body?.TechnicalReviewerSignaturePng ?? reviewerRow?.SignaturePng;
            if (string.IsNullOrWhiteSpace(reviewerSig))
                throw new InvalidOperationException(
                    "Technical reviewer signature missing — set your signature on your profile first.");
            var inspDate = r.InspectionDate ?? DateOnly.FromDateTime(_clock.UtcNow);
            // Per-Aramco-category validity — sourced from the Aramco reference sheet.
            // CR01/CR04/CR06 = 3 months; CR05/CR11 = 12 months; everything else = 6 months.
            var equipCat = await _db.Equipment.IgnoreQueryFilters()
                .Where(e => e.Id == r.EquipmentId).Select(e => e.AramcoCategory)
                .FirstOrDefaultAsync(ct) ?? AramcoCategory.None;
            var expiry = inspDate.AddMonths(equipCat.ValidityMonths());
            r.ApplyApprovalStamp(reviewer, reviewerSig,
                DateOnly.FromDateTime(_clock.UtcNow), expiry);

            if (r.StickerId is null)
            {
                var sticker = await _db.Stickers
                    .Where(s => s.State == StickerState.Unallocated)
                    .OrderBy(s => s.StickerNo)
                    .FirstOrDefaultAsync(ct)
                    ?? throw new InvalidOperationException(
                        "No Blue Sticker stock available. Manager: procure new stickers first.");
                sticker.Issue(r.Id, r.EquipmentId, r.ClientId, expiry, _clock.UtcNow);
                r.LinkSticker(sticker.Id, sticker.StickerNo);
            }
        }

        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}

public sealed class RequestClientOtpHandler
    : ICommandHandler<RequestClientOtpCommand, RequestClientOtpResponse>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOtpService _otp;
    private readonly IHostEnvironment _env;

    public RequestClientOtpHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IOtpService otp, IHostEnvironment env)
    { _db = db; _tenant = tenant; _clock = clock; _otp = otp; _env = env; }

    public async Task<RequestClientOtpResponse> Handle(
        RequestClientOtpCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(x => x.Transitions)
            : _db.BlueStickerReports.Include(x => x.Transitions);
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");

        var email = await _db.Clients.IgnoreQueryFilters()
            .Where(c => c.Id == r.ClientId).Select(c => c.ContactEmail)
            .FirstOrDefaultAsync(ct);
        if (string.IsNullOrWhiteSpace(email))
            throw new InvalidOperationException(
                "Client has no contact email on file — cannot send the signature OTP. " +
                "Set the client's contact email first.");

        var sm = new BlueStickerReportStateMachine(r, _tenant, _clock);
        if (!sm.CanFire(BlueStickerReportTrigger.RequestClientOtp))
            throw new InvalidOperationException(
                $"Cannot request a client OTP while report is in state {r.State}.");
        sm.Fire(BlueStickerReportTrigger.RequestClientOtp);

        var gen = _otp.Generate(_clock.UtcNow, TimeSpan.FromMinutes(15));
        r.SetClientOtp(gen.Hash, gen.ExpiresAtUtc, email!);
        await _otp.SendAsync(email!, gen.Code, gen.ExpiresAtUtc, r.Id, ct);

        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return new RequestClientOtpResponse(
            BlueStickerMapper.ToDetail(r),
            _env.IsDevelopment() ? gen.Code : null);
    }
}

public sealed class VerifyOtpAndSignHandler
    : ICommandHandler<VerifyOtpAndSignCommand, BlueStickerReportDetailDto>
{
    private const int MaxAttempts = 5;
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOtpService _otp;
    private readonly IValidator<VerifyOtpAndSignRequest> _validator;

    public VerifyOtpAndSignHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IOtpService otp, IValidator<VerifyOtpAndSignRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _otp = otp; _validator = validator; }

    public async Task<BlueStickerReportDetailDto> Handle(
        VerifyOtpAndSignCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.BlueStickerReports.IgnoreQueryFilters().Include(x => x.Transitions)
            : _db.BlueStickerReports.Include(x => x.Transitions);
        var r = await query.FirstOrDefaultAsync(x => x.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Report {command.Id} not found.");

        if (r.State != BlueStickerReportState.AwaitingClientSignature)
            throw new InvalidOperationException(
                $"Report is not awaiting a client signature (state {r.State}).");
        if (r.ClientOtpHash is null || r.ClientOtpExpiresAtUtc is null)
            throw new InvalidOperationException("No OTP has been requested for this report.");
        if (_clock.UtcNow > r.ClientOtpExpiresAtUtc)
            throw new InvalidOperationException("OTP has expired — request a new one.");
        if (r.ClientOtpAttempts >= MaxAttempts)
            throw new InvalidOperationException(
                "Too many incorrect attempts — request a new OTP.");

        if (!_otp.Verify(command.Body.Otp, r.ClientOtpHash))
        {
            r.RecordOtpAttempt();
            await _db.SaveChangesAsync(ct);
            throw new InvalidOperationException("Incorrect OTP.");
        }

        var sm = new BlueStickerReportStateMachine(r, _tenant, _clock);
        if (!sm.CanFire(BlueStickerReportTrigger.VerifyOtpAndSign))
            throw new InvalidOperationException(
                $"Cannot finalize from state {r.State}.");
        sm.Fire(BlueStickerReportTrigger.VerifyOtpAndSign);
        r.CaptureClientSignature(command.Body.ReceiverSignaturePng,
            DateOnly.FromDateTime(_clock.UtcNow));

        r.UpdatedAtUtc = _clock.UtcNow;
        r.UpdatedById = _tenant.UserId;
        await _db.SaveChangesAsync(ct);
        return BlueStickerMapper.ToDetail(r);
    }
}
