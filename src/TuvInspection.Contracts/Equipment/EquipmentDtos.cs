namespace TuvInspection.Contracts.Equipment;

public sealed record EquipmentTypeDto(
    Guid Id,
    string Name,
    AramcoCategoryDto? AramcoCategory,
    string? DefaultStandards,
    string? MsReference,
    string? Annex,
    bool IsActive);

public enum AramcoCategoryDto
{
    None = 0,
    CR01_MobileCrane = 1,
    CR02_ElevatorEscalator = 2,
    CR03_ElevationWorkPlatform = 3,
    CR04_MarineOffshoreCranes = 4,
    CR05_StorageRetrievalMachine = 5,
    CR06_ArticulatingBoomCrane = 6,
    CR07_LiftingSpreaderBeam = 7,
    CR08_PoweredPlatformSkyClimber = 8,
    CR09_VehicleMountedAerialDevice = 9,
    CR10_Manbasket = 10,
    CR11_FixedCranesHoists = 11,
    CR12_SideBoomTractor = 12,
    CR13_AFrameMobileGantry = 13,
    CR14_TowerCrane = 14
}

public enum EquipmentStatusDto { Active = 0, Decommissioned = 1, Sold = 2 }

public sealed record EquipmentListItemDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    AramcoCategoryDto? AramcoCategory,
    string IdNo,
    string? SerialNo,
    string? Manufacturer,
    string? Model,
    string? Swl,
    string? Location,
    EquipmentStatusDto Status);

public sealed record EquipmentDetailDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    AramcoCategoryDto? AramcoCategory,
    string IdNo,
    string? SerialNo,
    string? Manufacturer,
    string? Model,
    int? YearOfManufacture,
    string? Swl,
    string? Location,
    string? PhotoKey,
    EquipmentStatusDto Status,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateEquipmentRequest(
    Guid ClientId,
    Guid EquipmentTypeId,
    AramcoCategoryDto? AramcoCategory,
    string IdNo,
    string? SerialNo,
    string? Manufacturer,
    string? Model,
    int? YearOfManufacture,
    string? Swl,
    string? Location);

public sealed record UpdateEquipmentRequest(
    Guid EquipmentTypeId,
    AramcoCategoryDto? AramcoCategory,
    string IdNo,
    string? SerialNo,
    string? Manufacturer,
    string? Model,
    int? YearOfManufacture,
    string? Swl,
    string? Location,
    EquipmentStatusDto Status);

public sealed record EquipmentImportResult(int Imported, int Skipped, IReadOnlyList<string> Errors);
