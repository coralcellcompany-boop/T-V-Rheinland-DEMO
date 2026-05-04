using Microsoft.EntityFrameworkCore;
using TuvInspection.Domain.Equipment;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Equipment;

/// <summary>
/// Seeds the standard defect catalogue per SRS §6 H9. Defects are organised in two tiers:
/// generic defects (EquipmentTypeId == null, applicable to every type) and equipment-type-specific
/// defects (e.g. Mobile Crane wire-rope wear, Tower Crane jib distortion).
/// Idempotent — only inserts rows whose (EquipmentTypeId, Code) is not already present.
/// </summary>
public static class DefectCodeSeed
{
    public sealed record SeedRow(string? TypeName, string Code, string Description, string Severity);

    public static readonly IReadOnlyList<SeedRow> Rows = new SeedRow[]
    {
        // ── Generic defects (apply to all equipment types) ──────────────────────────
        new(null, "G-001", "Identification plate / nameplate missing or illegible",                "Major"),
        new(null, "G-002", "Manufacturer's documentation / manual not available on site",          "Minor"),
        new(null, "G-003", "Previous certificate / sticker expired",                               "Critical"),
        new(null, "G-004", "Operator licence / competency card not valid",                         "Critical"),
        new(null, "G-005", "Pre-use inspection log not maintained",                                "Major"),
        new(null, "G-006", "Visible structural damage (dents, cracks, deformation)",               "Critical"),
        new(null, "G-007", "Corrosion affecting load-bearing components",                          "Major"),
        new(null, "G-008", "Loose, missing or improperly tightened fasteners",                     "Major"),
        new(null, "G-009", "Hydraulic / pneumatic leaks observed",                                 "Major"),
        new(null, "G-010", "Warning labels and decals missing or unreadable",                      "Minor"),
        new(null, "G-011", "Emergency stop button non-functional",                                 "Critical"),
        new(null, "G-012", "Fire extinguisher missing, expired or insufficient capacity",          "Major"),
        new(null, "G-013", "Personal Protective Equipment (PPE) requirements not met",             "Major"),
        new(null, "G-014", "Modifications without manufacturer / engineer authorisation",          "Critical"),
        new(null, "G-015", "Annual / periodic maintenance overdue",                                "Major"),

        // ── Mobile Crane (Telescopic Boom) ──────────────────────────────────────────
        new("Mobile Crane (Telescopic Boom)", "MC-001", "Wire rope: broken wires exceed allowable per ASME B30.5", "Critical"),
        new("Mobile Crane (Telescopic Boom)", "MC-002", "Wire rope: corrosion, kinks or birdcaging",               "Critical"),
        new("Mobile Crane (Telescopic Boom)", "MC-003", "Hook: throat opening exceeds 15% of original",            "Critical"),
        new("Mobile Crane (Telescopic Boom)", "MC-004", "Hook safety latch missing or non-functional",             "Major"),
        new("Mobile Crane (Telescopic Boom)", "MC-005", "Load Moment Indicator (LMI) inoperative",                 "Critical"),
        new("Mobile Crane (Telescopic Boom)", "MC-006", "Anti-two-block device inoperative or bypassed",           "Critical"),
        new("Mobile Crane (Telescopic Boom)", "MC-007", "Boom angle indicator inaccurate or missing",              "Major"),
        new("Mobile Crane (Telescopic Boom)", "MC-008", "Outrigger pads missing or under-sized for ground",        "Major"),
        new("Mobile Crane (Telescopic Boom)", "MC-009", "Load chart not posted in operator's cab",                 "Major"),
        new("Mobile Crane (Telescopic Boom)", "MC-010", "Slewing brake / swing brake inoperative",                 "Critical"),

        // ── Tower Crane ─────────────────────────────────────────────────────────────
        new("Tower Crane", "TC-001", "Mast bolts loose or missing torque records",        "Critical"),
        new("Tower Crane", "TC-002", "Anemometer (wind speed sensor) inoperative",        "Critical"),
        new("Tower Crane", "TC-003", "Slewing limit switch malfunction",                  "Critical"),
        new("Tower Crane", "TC-004", "Hoist limit switch (upper / lower) malfunction",    "Critical"),
        new("Tower Crane", "TC-005", "Counter-jib ballast not as per manufacturer specs", "Critical"),
        new("Tower Crane", "TC-006", "Trolley travel limit switch inoperative",           "Major"),

        // ── Forklift Truck ──────────────────────────────────────────────────────────
        new("Forklift Truck", "FL-001", "Fork wear exceeds 10% (heel thickness)",         "Critical"),
        new("Forklift Truck", "FL-002", "Mast chains stretched / kinked / corroded",      "Critical"),
        new("Forklift Truck", "FL-003", "Overhead guard cracked or modified",             "Critical"),
        new("Forklift Truck", "FL-004", "Load backrest extension missing or damaged",     "Major"),
        new("Forklift Truck", "FL-005", "Seat belt missing or non-functional",            "Critical"),
        new("Forklift Truck", "FL-006", "Reverse alarm / horn inoperative",               "Major"),
        new("Forklift Truck", "FL-007", "Tilt cylinder leak or damaged seals",            "Major"),

        // ── Mobile Elevating Work Platform (MEWP) ───────────────────────────────────
        new("Mobile Elevating Work Platform (MEWP)", "MW-001", "Guard rails damaged or missing",                "Critical"),
        new("Mobile Elevating Work Platform (MEWP)", "MW-002", "Tilt sensor inoperative or bypassed",           "Critical"),
        new("Mobile Elevating Work Platform (MEWP)", "MW-003", "Outrigger interlock inoperative",               "Critical"),
        new("Mobile Elevating Work Platform (MEWP)", "MW-004", "Emergency lowering valve non-functional",       "Critical"),
        new("Mobile Elevating Work Platform (MEWP)", "MW-005", "Platform overload sensor inoperative",          "Critical"),

        // ── Manbasket ───────────────────────────────────────────────────────────────
        new("Manbasket", "MB-001", "Welds show cracking or porosity",                "Critical"),
        new("Manbasket", "MB-002", "Anchor points for fall arrest insufficient",     "Critical"),
        new("Manbasket", "MB-003", "Capacity plate missing or illegible",            "Major"),
        new("Manbasket", "MB-004", "Lanyard tie-off ring damaged",                   "Critical"),

        // ── Wire Rope Sling / Chain Sling / Webbing Sling (treat as separate entries) ─
        new("Wire Rope Sling", "WS-001", "Broken wires exceed 10 in one rope lay",   "Critical"),
        new("Wire Rope Sling", "WS-002", "Crushing or flattening of strands",        "Critical"),
        new("Wire Rope Sling", "WS-003", "Tag missing — capacity unknown",           "Critical"),
        new("Chain Sling",     "CS-001", "Stretch exceeds 5% of original length",    "Critical"),
        new("Chain Sling",     "CS-002", "Nicks or gouges in load-bearing links",    "Critical"),
        new("Webbing Sling",   "WB-001", "Cuts, holes or fraying of webbing",        "Critical"),
        new("Webbing Sling",   "WB-002", "Heat or chemical damage to webbing",       "Critical"),

        // ── Below the Hook Lifting Devices (spreader beams etc.) ────────────────────
        new("Below the Hook Lifting Devices", "BH-001", "Welds cracked or under-sized",           "Critical"),
        new("Below the Hook Lifting Devices", "BH-002", "Pin retainers missing",                  "Critical"),
        new("Below the Hook Lifting Devices", "BH-003", "Capacity / centre-of-gravity tag missing", "Major"),

        // ── Overhead Crane / Gantry Crane / Jib Crane (fixed cranes) ────────────────
        new("Overhead Crane", "OC-001", "End stops on rail damaged or missing",  "Critical"),
        new("Overhead Crane", "OC-002", "Festoon cable or busbar damaged",       "Major"),
        new("Overhead Crane", "OC-003", "Pendant control buttons sticking",      "Major"),
        new("Gantry Crane",   "GC-001", "Wheel flanges worn beyond limit",       "Critical"),
        new("Jib Crane",      "JC-001", "Slewing bearing play exceeds tolerance", "Critical"),

        // ── Elevator / Escalator ────────────────────────────────────────────────────
        new("Elevator", "EL-001", "Door interlock bypassed or non-functional",     "Critical"),
        new("Elevator", "EL-002", "Overspeed governor seal broken or unverified",  "Critical"),
        new("Elevator", "EL-003", "Buffer compression spring damaged",             "Major"),
        new("Escalator & Moving Walkway", "ES-001", "Comb plate teeth broken",     "Major"),
        new("Escalator & Moving Walkway", "ES-002", "Handrail speed sync deviation > 2%", "Major"),
    };

    public static async Task SeedAsync(AppDbContext db, CancellationToken ct)
    {
        var typesByName = await db.EquipmentTypes
            .ToDictionaryAsync(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);

        var existing = await db.DefectCodes
            .Select(d => new { d.EquipmentTypeId, d.Code })
            .ToListAsync(ct);
        var existingSet = new HashSet<(Guid?, string)>(
            existing.Select(x => (x.EquipmentTypeId, x.Code.ToUpperInvariant())));

        foreach (var row in Rows)
        {
            Guid? typeId = null;
            if (row.TypeName is not null)
            {
                if (!typesByName.TryGetValue(row.TypeName, out var resolved))
                    continue;
                typeId = resolved;
            }

            if (existingSet.Contains((typeId, row.Code.ToUpperInvariant())))
                continue;

            db.DefectCodes.Add(new DefectCode(
                Guid.NewGuid(), typeId, row.Code, row.Description, row.Severity));
        }
        await db.SaveChangesAsync(ct);
    }
}
