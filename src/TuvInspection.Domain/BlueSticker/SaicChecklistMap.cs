namespace TuvInspection.Domain.BlueSticker;

/// <summary>Authoritative (Aramco category short-code, equipment-type name) → SAIC-U-70##
/// inspection-checklist mapping. Source: "Aramco Category & Equipment Types.xlsx" (Blue Sticker
/// Services). Equipment-type names match the Aramco Annex 1 form's category dropdown verbatim.</summary>
public static class SaicChecklistMap
{
    private static readonly Dictionary<(string Cat, string Type), string> Map = new()
    {
        [("CR01", "Mobile Crane - All Terrain")] = "SAIC-U-7007",
        [("CR01", "Mobile Crane - Rough Terrain")] = "SAIC-U-7007",
        [("CR01", "Mobile Crane - Truck Mounted Crane")] = "SAIC-U-7007",
        [("CR01", "Mobile Crane - Boom Truck")] = "SAIC-U-7007",
        [("CR01", "Crawler Crane")] = "SAIC-U-7007",
        [("CR02", "Elevator")] = "SAIC-U-7005",
        [("CR02", "Escalator")] = "SAIC-U-7006",
        [("CR03", "Manlift - Boom Supported EWP")] = "SAIC-U-7013",
        [("CR03", "Scissor Lift - Self Propelled EWP")] = "SAIC-U-7014",
        [("CR03", "Manually Propelled EWP")] = "SAIC-U-7015",
        [("CR03", "Mast Climbing Personal Platform")] = "SAIC-U-7015",
        [("CR04", "Pedestal Crane")] = "SAIC-U-7018",
        [("CR04", "Pedestal Crane - Articulating Boom")] = "SAIC-U-7004",
        [("CR04", "Floating Crane - Articulating Boom")] = "SAIC-U-7004",
        [("CR04", "Floating Crane")] = "SAIC-U-7009",
        [("CR04", "Overhead Crane")] = "SAIC-U-7008",
        [("CR04", "Monorail Crane")] = "SAIC-U-7011",
        [("CR04", "Tower Crane")] = "SAIC-U-7003",
        [("CR04", "Portal Crane")] = "SAIC-U-7018",
        [("CR05", "Storage Retrieval Machine (SRM)")] = "SAIC-U-7012",
        [("CR06", "Articulating Boom Crane")] = "SAIC-U-7004",
        [("CR07", "Lifting Beam")] = "SAIC-U-7002",
        [("CR07", "Spreader Beam")] = "SAIC-U-7002",
        [("CR08", "Powered Platform / Sky Climber")] = "SAIC-U-7016",
        [("CR09", "Bucket Truck")] = "SAIC-U-7013",
        [("CR10", "Manbasket")] = "SAIC-U-7017",
        [("CR11", "Overhead Crane")] = "SAIC-U-7008",
        [("CR11", "Monorail Crane")] = "SAIC-U-7011",
        [("CR11", "Jib Crane")] = "SAIC-U-7011",
        [("CR12", "Side Boom Tractor")] = "SAIC-U-7010",
        [("CR13", "A-frame")] = "SAIC-U-7011",
        [("CR13", "Gantry Crane")] = "SAIC-U-7011",
        [("CR14", "Tower Crane")] = "SAIC-U-7003",
    };

    /// <summary>Resolve the SAIC number for a category + equipment-type name, or null if unmapped.</summary>
    public static string? Resolve(string? categoryShortCode, string? equipmentType)
    {
        if (string.IsNullOrWhiteSpace(categoryShortCode) || string.IsNullOrWhiteSpace(equipmentType))
            return null;
        return Map.TryGetValue((categoryShortCode.Trim(), equipmentType.Trim()), out var saic) ? saic : null;
    }

    /// <summary>All distinct SAIC numbers referenced by the mapping — used to assert catalog coverage.</summary>
    public static IReadOnlyCollection<string> AllSaicNumbers() => Map.Values.Distinct().ToList();
}
