using Microsoft.EntityFrameworkCore;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Equipment;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Equipment;

public sealed class ListEquipmentTypesHandler : IQueryHandler<ListEquipmentTypesQuery, IReadOnlyList<EquipmentTypeDto>>
{
    private readonly AppDbContext _db;
    public ListEquipmentTypesHandler(AppDbContext db) => _db = db;

    public async Task<IReadOnlyList<EquipmentTypeDto>> Handle(ListEquipmentTypesQuery q, CancellationToken ct)
    {
        var rows = await _db.EquipmentTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.Name)
            .ToListAsync(ct);
        return rows.Select(t => t.ToDto()).ToList();
    }
}
