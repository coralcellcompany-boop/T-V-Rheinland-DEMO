using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Equipment;

/// <summary>
/// Master data — one row per inspectable equipment type (56 in MVP per SRS §8.1).
/// Holds default reference standards, MS procedure ref, and Annex pointer.
/// </summary>
public class EquipmentType : Entity<Guid>
{
    public string Name { get; private set; } = default!;
    public AramcoCategory? AramcoCategory { get; private set; }
    public string? DefaultStandards { get; private set; }
    public string? MsReference { get; private set; }
    public string? Annex { get; private set; }
    public bool IsActive { get; private set; } = true;

    private EquipmentType() { }

    public EquipmentType(
        Guid id,
        string name,
        AramcoCategory? aramcoCategory,
        string? defaultStandards,
        string? msReference,
        string? annex) : base(id)
    {
        Name = name?.Trim() ?? throw new ArgumentNullException(nameof(name));
        AramcoCategory = aramcoCategory;
        DefaultStandards = defaultStandards?.Trim();
        MsReference = msReference?.Trim();
        Annex = annex?.Trim();
    }

    public void Deactivate() => IsActive = false;
    public void Reactivate() => IsActive = true;
}
