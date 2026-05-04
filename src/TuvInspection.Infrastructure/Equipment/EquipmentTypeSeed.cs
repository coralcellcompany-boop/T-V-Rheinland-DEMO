using Microsoft.EntityFrameworkCore;
using TuvInspection.Domain.Equipment;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Equipment;

/// <summary>
/// Seeds the 56 equipment types from SRS §8.1 (Master List 2025). Idempotent — only inserts
/// missing rows. Aramco category mapping is filled in from §8.2 where applicable; rows without
/// a CR-mapping are TPI-only and may still be Blue-Sticker-inspected if one is added later.
/// </summary>
public static class EquipmentTypeSeed
{
    public sealed record SeedRow(
        string Name,
        AramcoCategory? Category,
        string Standards,
        string MsRef,
        string Annex);

    public static readonly IReadOnlyList<SeedRow> Rows = new SeedRow[]
    {
        new("A-Frame",                                  AramcoCategory.CR13_AFrameMobileGantry,    "SASO-ISO 9927-5 / ASME B30.17",                                "MS-0025552", "Annex 4"),
        new("Air Compressor",                           null,                                       "ISO 1217 / ISO 8573 / ISO 5389",                               "MS-0048890", "Annex 6"),
        new("Articulating Boom Crane",                  AramcoCategory.CR06_ArticulatingBoomCrane,  "SASO GSO ISO 15442 / ASME B30.22",                             "MS-0044373", "Annex 2"),
        new("Base Mounted Drum Hoist",                  null,                                       "SASO GSO EN 14492-2 / ASME B30.7",                             "MS-0045046", "Annex 1"),
        new("Below the Hook Lifting Devices",           AramcoCategory.CR07_LiftingSpreaderBeam,    "SASO GSO EN 13155 & ASME B30.20",                              "MS-0045043", "Annex 1"),
        new("Cargo Carrying Units",                     null,                                       "SASO-GSO-ISO 10855-3",                                         "MS-0042557", "Annex 1"),
        new("Chain Block",                              null,                                       "ASME B30.16",                                                  "MS-0046675", "Annex 1"),
        new("Chain Sling",                              null,                                       "SASO GSO EN 818-2 / ASME B30.9",                               "MS-0045042", "Annex 3"),
        new("Elevator",                                 AramcoCategory.CR02_ElevatorEscalator,      "SASO-EN 81-20",                                                "MS-0043711", "Annex 3"),
        new("Escalator & Moving Walkway",               AramcoCategory.CR02_ElevatorEscalator,      "SASO-EN 115-1 & 2",                                            "MS-0043728", "Annex 3"),
        new("Eyebolt",                                  null,                                       "SASO ISO 3266",                                                "MS-0039281", "Annex 1"),
        new("Fire Fighting Truck",                      null,                                       "SASO-GSO-EN 1777",                                             "MS-0043800", "Annex 4"),
        new("Forklift Truck",                           null,                                       "SASO-GSO-ISO 5057 / ANSI B56.1",                               "MS-0042561", "Annex 1"),
        new("Gangway",                                  null,                                       "ISO-7061 / SOLAS",                                             "",           ""),
        new("Gantry Crane",                             AramcoCategory.CR13_AFrameMobileGantry,    "SASO-ISO 9927-5 / ASME B30.2",                                 "MS-0025552", "Annex 4"),
        new("Generator",                                null,                                       "EN-ISO 8528 / EN-ISO 12100 / EN 60034-1",                      "MS-0048890", "Annex 4"),
        new("Jack",                                     null,                                       "SASO GSO EN 1494 / ASME B30.1",                                "MS-0044367", "Annex 1"),
        new("Jib Crane",                                AramcoCategory.CR11_FixedCranesHoists,     "SASO-ISO 9927-5 / ASME B30.17",                                "MS-0025552", "Annex 2"),
        new("Ladder",                                   null,                                       "ISO 14122-3 / EN 131",                                         "MS-0048890", "Annex 8"),
        new("Lever Hoist",                              null,                                       "ASME B30.21",                                                  "MS-0046675", "Annex 2"),
        new("Lifeline Wire Rope System",                null,                                       "EN 795 / ANSI Z359",                                           "MS-0052855", "Annex 1"),
        new("Manbasket",                                AramcoCategory.CR10_Manbasket,             "ASME B30.23",                                                  "MS-0044363", "Annex 1"),
        new("Mast Climbing Work Platform",              AramcoCategory.CR03_ElevationWorkPlatform, "SASO GSO EN 1495 / ANSI A92.9",                                "MS-0043800", "Annex 2"),
        new("Mobile Crane (Telescopic Boom)",           AramcoCategory.CR01_MobileCrane,           "SASO-ISO 9927-1 / ASME B30.5",                                 "MS-0044364", "Annex 1"),
        new("Mobile Elevating Work Platform (MEWP)",    AramcoCategory.CR03_ElevationWorkPlatform, "SASO GSO EN 280 / ANSI A92.22",                                "MS-0043800", "Annex 1"),
        new("Mobile Machinery & Heavy Duty Equipment",  null,                                       "Per equipment sub-type",                                       "MS-0043812", "Annex 1"),
        new("Monorail Crane",                           AramcoCategory.CR11_FixedCranesHoists,     "SASO-ISO 9927-5 / ASME B30.17",                                "MS-0025552", "Annex 3"),
        new("Motorcycle",                               null,                                       "SASO-GSO-1798",                                                "MS-0051083", "Annex 1"),
        new("Overhead Crane",                           AramcoCategory.CR11_FixedCranesHoists,     "SASO-ISO 9927-5 / ASME B30.2 / B30.17",                        "MS-0025552", "Annex 1"),
        new("Pallet Lifter",                            null,                                       "SASO ISO 5057 / SASO ISO 3691-5",                              "MS-0042561", "Annex 2"),
        new("Petroleum Tanker",                         null,                                       "SASO-2288 / SASO-2809",                                        "MS-0045851", "Annex 1"),
        new("Power Tools",                              null,                                       "ISO 11148 / OSHA 29 CFR 1910.243 / 1926.303",                  "MS-0048890", "Annex 3"),
        new("Pump",                                     null,                                       "EN ISO 9906 / EN 809 / EN 12162",                              "MS-0048890", "Annex 5"),
        new("Rescue Tripod",                            null,                                       "EN 1496 / EN 795 / OSHA 29 CFR 1910.146",                      "MS-0052855", "Annex 3"),
        new("Rigging Hardware",                         null,                                       "ASME B30.26",                                                  "MS-0039281", "Annex 1"),
        new("Safety Harness & Lanyard",                 null,                                       "EN 361 / EN 365",                                              "MS-0052855", "Annex 2"),
        new("Scaffolding",                              null,                                       "SASO 329 / EN 12811",                                          "MS-0045159", "Annex 1"),
        new("Sky Climber / Cradle",                     AramcoCategory.CR08_PoweredPlatformSkyClimber, "ASME A120.1",                                              "MS-0044365", "Annex 1"),
        new("Storage Racking System",                   null,                                       "EN 15635 / BS-EN-15620 / ANSI MH16.1",                         "MS-0044440", "Annex 1"),
        new("Storage Retrieval Machine",                AramcoCategory.CR05_StorageRetrievalMachine, "SASO GSO EN 15095 / ASME B30.13",                              "MS-0048892", "Annex 1"),
        new("Telehandler / Reach Stacker",              null,                                       "SASO GSO 10896-1",                                             "MS-0042561", "Annex 2"),
        new("Tower Crane",                              AramcoCategory.CR14_TowerCrane,            "SASO-ISO 9927-3 / ASME B30.3",                                 "MS-0044362", "Annex 1"),
        new("Tower Light",                              null,                                       "BS-EN 60598 / BS 7671",                                        "MS-0048890", "Annex 2"),
        new("Trailer & Semi-Trailer",                   null,                                       "SASO-2910",                                                    "MS-0051082", "Annex 1"),
        new("Truck & Light Vehicle",                    null,                                       "SASO GSO ISO 3691-1",                                          "MS-0051082", "Annex 2"),
        new("Vehicle Lifter",                           null,                                       "SASO GSO EN 1493",                                             "MS-0044367", "Annex 2"),
        new("Water Tanker",                             null,                                       "SASO-2910 / SASO-GSO-25",                                      "MS-0045851", "Annex 2"),
        new("Webbing Sling",                            null,                                       "SASO GSO EN 1492-1/2 / ASME B30.9",                            "MS-0045042", "Annex 2"),
        new("Welding Machine",                          null,                                       "EN IEC 60974-1 / EN IEC 60974-4",                              "MS-0048890", "Annex 1"),
        new("Wire Rope Sling",                          null,                                       "SASO GSO EN 13414-1 / ASME B30.9",                             "MS-0045042", "Annex 1"),
        new("Zone II - Hazardous Area",                 null,                                       "IEC 60079-17 / BS-EN1834-1 / EN 1127-1",                       "MS-0047185", "Annex 1"),
        new("Bucket Truck (Vehicle Mounted Aerial)",    AramcoCategory.CR09_VehicleMountedAerialDevice, "SAIC-U-7013",                                              "MS-0043800", "Annex 3"),
        new("Side Boom Tractor",                        AramcoCategory.CR12_SideBoomTractor,       "SAIC-U-7010",                                                  "MS-0044364", "Annex 4"),
        new("Pedestal Crane",                           AramcoCategory.CR04_MarineOffshoreCranes,  "ASME B30.20 / API 2C",                                         "MS-0044362", "Annex 5"),
        new("Floating Crane",                           AramcoCategory.CR04_MarineOffshoreCranes,  "API Spec 2C",                                                  "MS-0044362", "Annex 6"),
        new("Portal & Pedestal Cranes",                 AramcoCategory.CR04_MarineOffshoreCranes,  "SAIC-U-7018",                                                  "MS-0044362", "Annex 7"),
    };

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        var existing = await db.EquipmentTypes
            .Select(e => e.Name)
            .ToListAsync(ct);
        var existingSet = new HashSet<string>(existing, StringComparer.OrdinalIgnoreCase);

        foreach (var row in Rows)
        {
            if (existingSet.Contains(row.Name)) continue;
            db.EquipmentTypes.Add(new EquipmentType(
                Guid.NewGuid(), row.Name, row.Category,
                row.Standards, row.MsRef, row.Annex));
        }
        await db.SaveChangesAsync(ct);
    }
}
