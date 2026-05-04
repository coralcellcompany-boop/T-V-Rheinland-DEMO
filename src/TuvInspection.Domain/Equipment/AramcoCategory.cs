namespace TuvInspection.Domain.Equipment;

/// <summary>
/// Aramco equipment categories CR01 through CR14 per SRS §8.2.
/// Drives validity periods and SAIC checklist mapping for Blue Sticker service.
/// </summary>
public enum AramcoCategory
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

public enum EquipmentStatus
{
    Active = 0,
    Decommissioned = 1,
    Sold = 2
}
