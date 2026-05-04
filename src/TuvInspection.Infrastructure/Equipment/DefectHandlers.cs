using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Equipment;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Equipment;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Equipment;

public sealed class ListDefectCodesHandler : IQueryHandler<ListDefectCodesQuery, IReadOnlyList<DefectCodeDto>>
{
    private readonly AppDbContext _db;
    public ListDefectCodesHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<DefectCodeDto>> Handle(ListDefectCodesQuery q, CancellationToken ct)
    {
        var query = from d in _db.DefectCodes.AsNoTracking()
                    join t in _db.EquipmentTypes.AsNoTracking()
                        on d.EquipmentTypeId equals t.Id into tj
                    from t in tj.DefaultIfEmpty()
                    select new { d, TypeName = t != null ? t.Name : null };

        if (!q.IncludeInactive)
            query = query.Where(x => x.d.IsActive);

        if (q.EquipmentTypeId is { } typeId)
            query = query.Where(x => x.d.EquipmentTypeId == null || x.d.EquipmentTypeId == typeId);

        var rows = await query
            .OrderBy(x => x.d.EquipmentTypeId == null ? 0 : 1)
            .ThenBy(x => x.TypeName)
            .ThenBy(x => x.d.Code)
            .ToListAsync(ct);

        return rows.Select(x => new DefectCodeDto(
            x.d.Id, x.d.EquipmentTypeId, x.TypeName,
            x.d.Code, x.d.Description, x.d.Severity, x.d.IsActive)).ToList();
    }
}

public sealed class CreateDefectCodeHandler : ICommandHandler<CreateDefectCodeCommand, DefectCodeDto>
{
    private readonly AppDbContext _db;
    public CreateDefectCodeHandler(AppDbContext db) => _db = db;

    public async Task<DefectCodeDto> Handle(CreateDefectCodeCommand c, CancellationToken ct)
    {
        var entity = new DefectCode(Guid.NewGuid(), c.Body.EquipmentTypeId,
            c.Body.Code, c.Body.Description, c.Body.Severity);
        _db.DefectCodes.Add(entity);
        await _db.SaveChangesAsync(ct);

        var typeName = entity.EquipmentTypeId is { } id
            ? await _db.EquipmentTypes.AsNoTracking().Where(t => t.Id == id).Select(t => t.Name).FirstOrDefaultAsync(ct)
            : null;

        return new DefectCodeDto(entity.Id, entity.EquipmentTypeId, typeName,
            entity.Code, entity.Description, entity.Severity, entity.IsActive);
    }
}

public sealed class UpdateDefectCodeHandler : ICommandHandler<UpdateDefectCodeCommand, DefectCodeDto>
{
    private readonly AppDbContext _db;
    public UpdateDefectCodeHandler(AppDbContext db) => _db = db;

    public async Task<DefectCodeDto> Handle(UpdateDefectCodeCommand c, CancellationToken ct)
    {
        var entity = await _db.DefectCodes.FirstOrDefaultAsync(d => d.Id == c.Id, ct)
            ?? throw new KeyNotFoundException($"Defect code {c.Id} not found.");

        entity.Update(c.Body.Code, c.Body.Description, c.Body.Severity, c.Body.IsActive);
        await _db.SaveChangesAsync(ct);

        var typeName = entity.EquipmentTypeId is { } id
            ? await _db.EquipmentTypes.AsNoTracking().Where(t => t.Id == id).Select(t => t.Name).FirstOrDefaultAsync(ct)
            : null;

        return new DefectCodeDto(entity.Id, entity.EquipmentTypeId, typeName,
            entity.Code, entity.Description, entity.Severity, entity.IsActive);
    }
}
