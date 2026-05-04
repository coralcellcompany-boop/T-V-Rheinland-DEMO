using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Equipment;

namespace TuvInspection.Infrastructure.Equipment;

internal static class EquipmentMappers
{
    public static EquipmentTypeDto ToDto(this EquipmentType t) =>
        new(t.Id, t.Name, (AramcoCategoryDto?)t.AramcoCategory,
            t.DefaultStandards, t.MsReference, t.Annex, t.IsActive);
}
