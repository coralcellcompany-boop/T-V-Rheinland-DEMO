using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Certificates;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.Certificates;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Stickers;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Certificates;

public sealed class ListCertificatesHandler
    : IQueryHandler<ListCertificatesQuery, PagedResult<CertificateListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListCertificatesHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<PagedResult<CertificateListItemDto>> Handle(
        ListCertificatesQuery q, CancellationToken ct)
    {
        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();

        if (q.ClientId is { } cid) certs = certs.Where(c => c.ClientId == cid);
        if (q.EquipmentId is { } eid) certs = certs.Where(c => c.EquipmentId == eid);
        if (q.State is { } st) certs = certs.Where(c => c.State == (CertificateState)(int)st);
        if (q.InspectionType is { } it) certs = certs.Where(c => c.InspectionType == (CertificateInspectionType)(int)it);
        if (q.Result is { } r) certs = certs.Where(c => c.Result == (InspectionResult)(int)r);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            certs = certs.Where(c => EF.Functions.Like(c.CertificateNo, $"%{s}%"));
        }

        var total = await certs.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var items = await certs
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new { c, cl })
            .Join(_db.Equipment.IgnoreQueryFilters(), x => x.c.EquipmentId, e => e.Id, (x, e) => new { x.c, x.cl, e })
            .Join(_db.EquipmentTypes, x => x.e.EquipmentTypeId, t => t.Id, (x, t) => new CertificateListItemDto(
                x.c.Id, x.c.CertificateNo, x.c.ClientId, x.cl.Name,
                x.c.EquipmentId, x.e.IdNo, t.Name,
                x.c.InspectionDate, x.c.NextDueDate,
                (CertificateInspectionTypeDto)x.c.InspectionType,
                (LoadTestKindDto)x.c.LoadTest,
                (InspectionResultDto)x.c.Result,
                (CertificateStateDto)x.c.State,
                x.c.StickerNo, x.c.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CertificateListItemDto>(items, total, page, pageSize);
    }
}

public sealed class GetCertificateByIdHandler
    : IQueryHandler<GetCertificateByIdQuery, CertificateDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetCertificateByIdHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<CertificateDetailDto?> Handle(GetCertificateByIdQuery q, CancellationToken ct)
    {
        IQueryable<InspectionCertificate> baseQuery = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().Include(c => c.Transitions)
            : _db.Certificates.Include(c => c.Transitions);

        var c = await baseQuery.AsNoTracking().FirstOrDefaultAsync(x => x.Id == q.Id, ct);
        if (c is null) return null;

        var clientName = await _db.Clients.IgnoreQueryFilters()
            .Where(cl => cl.Id == c.ClientId).Select(cl => cl.Name).FirstAsync(ct);
        var equip = await _db.Equipment.IgnoreQueryFilters()
            .Where(e => e.Id == c.EquipmentId)
            .Join(_db.EquipmentTypes, e => e.EquipmentTypeId, t => t.Id,
                (e, t) => new { e.IdNo, TypeId = t.Id, TypeName = t.Name })
            .FirstAsync(ct);

        return new CertificateDetailDto(
            c.Id, c.CertificateNo, c.ClientId, clientName,
            c.EquipmentId, equip.IdNo, equip.TypeId, equip.TypeName,
            c.JobOrderId, c.InspectionDate, c.ReportIssueDate, c.NextDueDate,
            (CertificateInspectionTypeDto)c.InspectionType,
            (LoadTestKindDto)c.LoadTest,
            (InspectionResultDto)c.Result,
            (CertificateStateDto)c.State,
            c.Standards, c.StickerNo,
            c.ChecklistJson, c.FindingsJson, c.PhotosJson, c.SignaturesJson,
            c.CreatedAtUtc, c.UpdatedAtUtc,
            c.Transitions.OrderBy(t => t.AtUtc).Select(t => t.ToDto()).ToList());
    }
}

public sealed class GetApprovalQueueCountsHandler
    : IQueryHandler<GetApprovalQueueCountsQuery, ApprovalQueueCountsDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetApprovalQueueCountsHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<ApprovalQueueCountsDto> Handle(GetApprovalQueueCountsQuery q, CancellationToken ct)
    {
        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();

        var pending = await certs.CountAsync(c =>
            c.State == CertificateState.Submitted ||
            c.State == CertificateState.UnderReview ||
            c.State == CertificateState.AwaitingApproval, ct);
        var rejected = await certs.CountAsync(c => c.State == CertificateState.Rejected, ct);
        int mine = 0;
        if (_tenant.UserId is { } uid)
        {
            mine = await certs.CountAsync(c => c.CreatedById == uid, ct);
        }
        return new ApprovalQueueCountsDto(pending, rejected, mine);
    }
}

public sealed class ListApprovalQueueHandler
    : IQueryHandler<ListApprovalQueueQuery, PagedResult<CertificateListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListApprovalQueueHandler(AppDbContext db, ITenantContext tenant)
    { _db = db; _tenant = tenant; }

    public async Task<PagedResult<CertificateListItemDto>> Handle(ListApprovalQueueQuery q, CancellationToken ct)
    {
        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();

        certs = q.Bucket switch
        {
            "pending" => certs.Where(c => c.State == CertificateState.Submitted
                || c.State == CertificateState.UnderReview
                || c.State == CertificateState.AwaitingApproval),
            "rejected" => certs.Where(c => c.State == CertificateState.Rejected),
            "mine" when _tenant.UserId is { } uid => certs.Where(c => c.CreatedById == uid),
            _ => certs.Where(c => false),
        };

        var total = await certs.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var items = await certs
            .OrderByDescending(c => c.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new { c, cl })
            .Join(_db.Equipment.IgnoreQueryFilters(), x => x.c.EquipmentId, e => e.Id, (x, e) => new { x.c, x.cl, e })
            .Join(_db.EquipmentTypes, x => x.e.EquipmentTypeId, t => t.Id, (x, t) => new CertificateListItemDto(
                x.c.Id, x.c.CertificateNo, x.c.ClientId, x.cl.Name,
                x.c.EquipmentId, x.e.IdNo, t.Name,
                x.c.InspectionDate, x.c.NextDueDate,
                (CertificateInspectionTypeDto)x.c.InspectionType,
                (LoadTestKindDto)x.c.LoadTest,
                (InspectionResultDto)x.c.Result,
                (CertificateStateDto)x.c.State,
                x.c.StickerNo, x.c.CreatedAtUtc))
            .ToListAsync(ct);

        return new PagedResult<CertificateListItemDto>(items, total, page, pageSize);
    }
}

public sealed class CreateCertificateHandler : ICommandHandler<CreateCertificateCommand, CertificateDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateCertificateRequest> _validator;
    private readonly CertificateNoGenerator _no;

    public CreateCertificateHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<CreateCertificateRequest> validator, CertificateNoGenerator no)
    {
        _db = db; _tenant = tenant; _clock = clock; _validator = validator; _no = no;
    }

    public async Task<CertificateDetailDto> Handle(CreateCertificateCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var equip = await _db.Equipment.IgnoreQueryFilters()
            .FirstOrDefaultAsync(e => e.Id == command.Body.EquipmentId, ct)
            ?? throw new ArgumentException("Equipment not found.");

        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(equip.ClientId))
            throw new UnauthorizedAccessException("You can only create certificates under clients you are assigned to.");

        var certNo = await _no.Next(ct);

        var cert = new InspectionCertificate(
            Guid.NewGuid(), certNo,
            equip.ClientId, equip.Id, command.Body.JobOrderId,
            command.Body.InspectionDate, command.Body.ReportIssueDate,
            (CertificateInspectionType)command.Body.InspectionType);

        if (!string.IsNullOrWhiteSpace(command.Body.Standards))
            cert.UpdateInspectionData(command.Body.Standards, LoadTestKind.None, InspectionResult.NotSet, null, null);

        cert.CreatedAtUtc = _clock.UtcNow;
        cert.CreatedById = _tenant.UserId;

        _db.Certificates.Add(cert);
        await _db.SaveChangesAsync(ct);

        return (await new GetCertificateByIdHandler(_db, _tenant).Handle(new(cert.Id), ct))!;
    }
}

public sealed class UpdateCertificateHandler : ICommandHandler<UpdateCertificateCommand, CertificateDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateCertificateRequest> _validator;

    public UpdateCertificateHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateCertificateRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<CertificateDetailDto> Handle(UpdateCertificateCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters()
            : _db.Certificates;
        var cert = await query.FirstOrDefaultAsync(c => c.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Certificate {command.Id} not found.");

        if (cert.State != CertificateState.Draft && cert.State != CertificateState.Rejected)
            throw new InvalidOperationException(
                $"Certificate cannot be edited in state {cert.State}. Only Draft and Rejected are mutable.");

        cert.UpdateInspectionData(
            command.Body.Standards,
            (LoadTestKind)(int)command.Body.LoadTest,
            (InspectionResult)(int)command.Body.Result,
            command.Body.NextDueDate,
            command.Body.StickerNo);
        cert.UpdateChecklist(command.Body.ChecklistJson);
        cert.UpdateFindings(command.Body.FindingsJson);
        cert.UpdatePhotos(command.Body.PhotosJson);
        cert.UpdateSignatures(command.Body.SignaturesJson);

        cert.UpdatedAtUtc = _clock.UtcNow;
        cert.UpdatedById = _tenant.UserId;

        await _db.SaveChangesAsync(ct);
        return (await new GetCertificateByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed class FireCertificateTriggerHandler : ICommandHandler<FireCertificateTriggerCommand, CertificateDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IOutbox _outbox;

    public FireCertificateTriggerHandler(AppDbContext db, ITenantContext tenant, IClock clock, IOutbox outbox)
    { _db = db; _tenant = tenant; _clock = clock; _outbox = outbox; }

    public async Task<CertificateDetailDto> Handle(FireCertificateTriggerCommand command, CancellationToken ct)
    {
        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().Include(c => c.Transitions)
            : _db.Certificates.Include(c => c.Transitions);

        var cert = await query.FirstOrDefaultAsync(c => c.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Certificate {command.Id} not found.");

        var trigger = (CertificateTrigger)(int)command.Trigger;
        var sm = new CertificateStateMachine(cert, _tenant, _clock);
        if (!sm.CanFire(trigger))
            throw new InvalidOperationException(
                $"Cannot {trigger} a certificate currently in state {cert.State}.");

        sm.Fire(trigger, command.Comments);

        // Auto-issue Blue Sticker: when a cert with an Aramco-categorized equipment hits
        // Approved, pull the next Unallocated sticker and link it. If stock is empty we
        // surface a clear error so the manager knows to procure more.
        if (cert.State == CertificateState.Approved && cert.StickerId is null)
        {
            var equipCat = await _db.Equipment.IgnoreQueryFilters()
                .Where(e => e.Id == cert.EquipmentId)
                .Select(e => e.AramcoCategory)
                .FirstOrDefaultAsync(ct);

            if (equipCat is not null && equipCat != 0)
            {
                var sticker = await _db.Stickers
                    .Where(s => s.State == StickerState.Unallocated)
                    .OrderBy(s => s.StickerNo)
                    .FirstOrDefaultAsync(ct);
                if (sticker is null)
                    throw new InvalidOperationException(
                        "No Blue Sticker stock available. Manager: procure new stickers first.");
                sticker.Issue(cert.Id, cert.EquipmentId, cert.ClientId,
                    cert.NextDueDate, _clock.UtcNow);
                cert.LinkSticker(sticker.Id, sticker.StickerNo);
            }
        }

        // Side-effect outbox: when certificate moves to ClientSent, queue an email payload.
        if (cert.State == CertificateState.ClientSent)
        {
            await _outbox.Enqueue(new ClientSentCertificateEmail(
                cert.Id, cert.CertificateNo, cert.ClientId, _clock.UtcNow), ct);
        }

        await _db.SaveChangesAsync(ct);
        return (await new GetCertificateByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}

public sealed record ClientSentCertificateEmail(Guid CertificateId, string CertificateNo, Guid ClientId, DateTime AtUtc);
