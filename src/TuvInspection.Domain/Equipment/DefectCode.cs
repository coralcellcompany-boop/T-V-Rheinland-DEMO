using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Equipment;

/// <summary>
/// Defect catalogue entry per SRS §6 H9. Optionally scoped to a single equipment type;
/// when EquipmentTypeId is null the defect is generic and shown for all types.
/// </summary>
public class DefectCode : Entity<Guid>
{
    public Guid? EquipmentTypeId { get; private set; }
    public string Code { get; private set; } = default!;
    public string Description { get; private set; } = default!;
    public string Severity { get; private set; } = "Minor"; // Minor / Major / Critical
    public bool IsActive { get; private set; } = true;

    private DefectCode() { }

    public DefectCode(Guid id, Guid? equipmentTypeId, string code, string description, string severity)
        : base(id)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        EquipmentTypeId = equipmentTypeId;
        Code = code.Trim().ToUpperInvariant();
        Description = description.Trim();
        Severity = string.IsNullOrWhiteSpace(severity) ? "Minor" : severity.Trim();
    }

    public void Update(string code, string description, string severity, bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.", nameof(code));
        if (string.IsNullOrWhiteSpace(description)) throw new ArgumentException("Description is required.", nameof(description));
        Code = code.Trim().ToUpperInvariant();
        Description = description.Trim();
        Severity = string.IsNullOrWhiteSpace(severity) ? "Minor" : severity.Trim();
        IsActive = isActive;
    }
}
