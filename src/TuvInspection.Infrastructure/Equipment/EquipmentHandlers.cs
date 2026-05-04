using FluentValidation;
using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Common.Time;
using TuvInspection.Application.Equipment;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;
using AramcoCategory = TuvInspection.Domain.Equipment.AramcoCategory;
using EquipmentStatus = TuvInspection.Domain.Equipment.EquipmentStatus;

namespace TuvInspection.Infrastructure.Equipment;

public sealed class ListEquipmentHandler : IQueryHandler<ListEquipmentQuery, PagedResult<EquipmentListItemDto>>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public ListEquipmentHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<PagedResult<EquipmentListItemDto>> Handle(ListEquipmentQuery q, CancellationToken ct)
    {
        // Manager bypass — explicit, not implicit through the filter.
        // Comment: tenant filter is bypassed only for Manager role; everyone else relies on the
        // EF global query filter scoped by AssignedClientIds via ITenantContext.
        IQueryable<EquipmentEntity> query = _tenant.IsInRole(Roles.Manager)
            ? _db.Equipment.IgnoreQueryFilters().AsNoTracking()
            : _db.Equipment.AsNoTracking();

        if (q.ClientId is { } cid) query = query.Where(e => e.ClientId == cid);
        if (q.EquipmentTypeId is { } tid) query = query.Where(e => e.EquipmentTypeId == tid);
        if (q.AramcoCategory is { } cat) query = query.Where(e => e.AramcoCategory == (AramcoCategory)(int)cat);
        if (q.Status is { } st) query = query.Where(e => e.Status == (EquipmentStatus)(int)st);
        if (!string.IsNullOrWhiteSpace(q.Search))
        {
            var s = q.Search.Trim();
            query = query.Where(e =>
                EF.Functions.Like(e.IdNo, $"%{s}%") ||
                (e.SerialNo != null && EF.Functions.Like(e.SerialNo, $"%{s}%")) ||
                (e.Manufacturer != null && EF.Functions.Like(e.Manufacturer, $"%{s}%")));
        }

        var total = await query.CountAsync(ct);
        var page = Math.Max(1, q.Page);
        var pageSize = Math.Clamp(q.PageSize, 1, 200);

        var items = await query
            .OrderByDescending(e => e.CreatedAtUtc)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Join(_db.Clients.IgnoreQueryFilters(), e => e.ClientId, c => c.Id, (e, c) => new { e, c })
            .Join(_db.EquipmentTypes, ec => ec.e.EquipmentTypeId, t => t.Id, (ec, t) => new EquipmentListItemDto(
                ec.e.Id, ec.e.ClientId, ec.c.Name,
                ec.e.EquipmentTypeId, t.Name,
                (AramcoCategoryDto?)ec.e.AramcoCategory,
                ec.e.IdNo, ec.e.SerialNo, ec.e.Manufacturer, ec.e.Model, ec.e.Swl, ec.e.Location,
                (EquipmentStatusDto)ec.e.Status))
            .ToListAsync(ct);

        return new PagedResult<EquipmentListItemDto>(items, total, page, pageSize);
    }
}

public sealed class GetEquipmentByIdHandler : IQueryHandler<GetEquipmentByIdQuery, EquipmentDetailDto?>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    public GetEquipmentByIdHandler(AppDbContext db, ITenantContext tenant) { _db = db; _tenant = tenant; }

    public async Task<EquipmentDetailDto?> Handle(GetEquipmentByIdQuery q, CancellationToken ct)
    {
        IQueryable<EquipmentEntity> baseQuery = _tenant.IsInRole(Roles.Manager)
            ? _db.Equipment.IgnoreQueryFilters().AsNoTracking()
            : _db.Equipment.AsNoTracking();

        var row = await baseQuery
            .Where(e => e.Id == q.Id)
            .Join(_db.Clients.IgnoreQueryFilters(), e => e.ClientId, c => c.Id, (e, c) => new { e, c })
            .Join(_db.EquipmentTypes, ec => ec.e.EquipmentTypeId, t => t.Id, (ec, t) => new EquipmentDetailDto(
                ec.e.Id, ec.e.ClientId, ec.c.Name,
                ec.e.EquipmentTypeId, t.Name,
                (AramcoCategoryDto?)ec.e.AramcoCategory,
                ec.e.IdNo, ec.e.SerialNo, ec.e.Manufacturer, ec.e.Model,
                ec.e.YearOfManufacture, ec.e.Swl, ec.e.Location, ec.e.PhotoKey,
                (EquipmentStatusDto)ec.e.Status, ec.e.CreatedAtUtc, ec.e.UpdatedAtUtc))
            .FirstOrDefaultAsync(ct);

        return row;
    }
}

public sealed class CreateEquipmentHandler : ICommandHandler<CreateEquipmentCommand, EquipmentDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<CreateEquipmentRequest> _validator;
    public CreateEquipmentHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<CreateEquipmentRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<EquipmentDetailDto> Handle(CreateEquipmentCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(command.Body.ClientId))
            throw new UnauthorizedAccessException("You can only create equipment under clients you are assigned to.");

        var typeExists = await _db.EquipmentTypes.AnyAsync(t => t.Id == command.Body.EquipmentTypeId, ct);
        if (!typeExists) throw new ArgumentException("Unknown equipment type.");

        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == command.Body.ClientId, ct);
        if (!clientExists) throw new ArgumentException("Unknown client.");

        var dup = await _db.Equipment.IgnoreQueryFilters()
            .AnyAsync(e => e.ClientId == command.Body.ClientId
                && e.IdNo == command.Body.IdNo, ct);
        if (dup) throw new ArgumentException($"Equipment with ID '{command.Body.IdNo}' already exists for this client.");

        var entity = new EquipmentEntity(
            Guid.NewGuid(),
            command.Body.ClientId,
            command.Body.EquipmentTypeId,
            command.Body.IdNo,
            (AramcoCategory?)command.Body.AramcoCategory);

        entity.UpdateIdentification(command.Body.IdNo, command.Body.SerialNo);
        entity.UpdateSpec(command.Body.Manufacturer, command.Body.Model, command.Body.YearOfManufacture, command.Body.Swl);
        entity.UpdateLocation(command.Body.Location);
        entity.CreatedAtUtc = _clock.UtcNow;
        entity.CreatedById = _tenant.UserId;

        _db.Equipment.Add(entity);
        await _db.SaveChangesAsync(ct);

        return (await new GetEquipmentByIdHandler(_db, _tenant).Handle(new(entity.Id), ct))!;
    }
}

public sealed class UpdateEquipmentHandler : ICommandHandler<UpdateEquipmentCommand, EquipmentDetailDto>
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly IValidator<UpdateEquipmentRequest> _validator;
    public UpdateEquipmentHandler(AppDbContext db, ITenantContext tenant, IClock clock,
        IValidator<UpdateEquipmentRequest> validator)
    { _db = db; _tenant = tenant; _clock = clock; _validator = validator; }

    public async Task<EquipmentDetailDto> Handle(UpdateEquipmentCommand command, CancellationToken ct)
    {
        await _validator.ValidateAndThrowAsync(command.Body, ct);

        var query = _tenant.IsInRole(Roles.Manager)
            ? _db.Equipment.IgnoreQueryFilters()
            : _db.Equipment;

        var entity = await query.FirstOrDefaultAsync(e => e.Id == command.Id, ct)
            ?? throw new KeyNotFoundException($"Equipment {command.Id} not found.");

        entity.UpdateIdentification(command.Body.IdNo, command.Body.SerialNo);
        entity.UpdateSpec(command.Body.Manufacturer, command.Body.Model, command.Body.YearOfManufacture, command.Body.Swl);
        entity.UpdateLocation(command.Body.Location);
        entity.SetPhoto(command.Body.PhotoKey);
        switch (command.Body.Status)
        {
            case EquipmentStatusDto.Active: entity.Reactivate(); break;
            case EquipmentStatusDto.Decommissioned: entity.Decommission(); break;
            case EquipmentStatusDto.Sold: entity.MarkSold(); break;
        }
        entity.UpdatedAtUtc = _clock.UtcNow;
        entity.UpdatedById = _tenant.UserId;

        await _db.SaveChangesAsync(ct);
        return (await new GetEquipmentByIdHandler(_db, _tenant).Handle(new(command.Id), ct))!;
    }
}
