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

/// <summary>
/// Aramco-mandated metadata per category. Source: Saudi Aramco "Aramco Category & Equipment
/// Types" reference sheet (validity column) + the SAIC-U-7001…7018 checklist series.
/// </summary>
public static class AramcoCategoryInfo
{
    /// <summary>Short code as it appears on the Annex 1 sheet and Aramco weekly tracker
    /// (e.g. "CR10", not "CR10_Manbasket").</summary>
    public static string ShortCode(this AramcoCategory c) => c switch
    {
        AramcoCategory.None => "",
        _ => c.ToString().Split('_')[0],
    };

    /// <summary>Per-category SAIC-U-70## inspection checklist number. Sourced from the Aramco
    /// "Inspection Checklist Number" column. Where the category covers multiple equipment types
    /// each with a different checklist (CR02, CR03, CR04, CR11), we return the most general
    /// checklist for the category and the inspector picks the specific one per equipment.</summary>
    public static string? ChecklistNumber(this AramcoCategory c) => c switch
    {
        AramcoCategory.CR01_MobileCrane => "SAIC-U-7007",
        AramcoCategory.CR02_ElevatorEscalator => "SAIC-U-7005",
        AramcoCategory.CR03_ElevationWorkPlatform => "SAIC-U-7013",
        AramcoCategory.CR04_MarineOffshoreCranes => "SAIC-U-7018",
        AramcoCategory.CR05_StorageRetrievalMachine => "SAIC-U-7012",
        AramcoCategory.CR06_ArticulatingBoomCrane => "SAIC-U-7004",
        AramcoCategory.CR07_LiftingSpreaderBeam => "SAIC-U-7002",
        AramcoCategory.CR08_PoweredPlatformSkyClimber => "SAIC-U-7016",
        AramcoCategory.CR09_VehicleMountedAerialDevice => "SAIC-U-7013",
        AramcoCategory.CR10_Manbasket => "SAIC-U-7017",
        AramcoCategory.CR11_FixedCranesHoists => "SAIC-U-7008",
        AramcoCategory.CR12_SideBoomTractor => "SAIC-U-7010",
        AramcoCategory.CR13_AFrameMobileGantry => "SAIC-U-7008",
        AramcoCategory.CR14_TowerCrane => "SAIC-U-7003",
        _ => null,
    };

    /// <summary>Blue Sticker validity in months — drives the next inspection due date stamped
    /// on the sticker when the report is approved.</summary>
    public static int ValidityMonths(this AramcoCategory c) => c switch
    {
        AramcoCategory.CR01_MobileCrane => 3,
        AramcoCategory.CR04_MarineOffshoreCranes => 3,
        AramcoCategory.CR06_ArticulatingBoomCrane => 3,
        AramcoCategory.CR05_StorageRetrievalMachine => 12,
        AramcoCategory.CR11_FixedCranesHoists => 12,
        AramcoCategory.None => 12,
        // All other categories default to 6 months per the Aramco reference sheet.
        _ => 6,
    };
}
