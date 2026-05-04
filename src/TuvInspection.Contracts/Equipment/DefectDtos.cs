namespace TuvInspection.Contracts.Equipment;

public sealed record DefectCodeDto(
    Guid Id,
    Guid? EquipmentTypeId,
    string? EquipmentTypeName,
    string Code,
    string Description,
    string Severity,
    bool IsActive);

public sealed record CreateDefectCodeRequest(
    Guid? EquipmentTypeId,
    string Code,
    string Description,
    string Severity);

public sealed record UpdateDefectCodeRequest(
    string Code,
    string Description,
    string Severity,
    bool IsActive);
