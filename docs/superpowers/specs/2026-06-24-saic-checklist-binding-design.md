# SAIC Checklist Binding — Design

**Date:** 2026-06-24
**Branch:** feature/blue-sticker-workflow
**Status:** Approved for spec review

## Problem

When an inspector creates a Blue Sticker inspection, they pick an **Equipment Type**. Each
equipment type maps to a Saudi Aramco inspection checklist (`SAIC-U-70##`). The inspector
must then fill that specific checklist (Pass / Fail / N/A + remark per item) and the result
is stored on the report.

Today the system does **not** do this:

1. **Checklist number is keyed by Aramco *category*, not equipment *type*.** One SAIC number
   per CR01–CR14. But the Aramco reference sheet keys it per **equipment type** — e.g. CR04
   alone spans 8 different checklists (7018, 7004, 7009, 7008, 7011, 7003). Code today:
   - `AramcoCategoryInfo.ChecklistNumber()` — [AramcoCategory.cs:51](../../../src/TuvInspection.Domain/Equipment/AramcoCategory.cs#L51)
   - `BlueStickerMapper.ChecklistFor()` — [BlueStickerHandlers.cs:39](../../../src/TuvInspection.Infrastructure/BlueSticker/BlueStickerHandlers.cs#L39)

2. **Checklist *content* is generic and invented, not the real SAIC items.** The editor has
   only 3 regex-matched templates (crane / pressure / electrical) + 1 load-test template —
   [checklist-editor.component.ts:62](../../../web/src/app/features/certificates/components/checklist-editor.component.ts#L62).
   The real ~700 items across the 18 SAIC checklists exist only in the source PDFs at
   `SOFTWARE IMPLEMENTATION/Blue Sticker Inspection Service/SIAC 7001-7018/`, nowhere in code.

## Goal

Selecting an equipment type → resolves the correct `SAIC-U-70##` → loads that checklist's
**real** items into the existing checklist editor → inspector fills it → stored on the report.
No change to the editor's data shape — the PDFs already match it 1:1
(Item No / Acceptance Criteria / Reference / Pass-Fail-NA / Remark).

## Non-Goals

- Not building 18 bespoke UIs — one editor, data-driven by the loaded catalog entry.
- Not making the source PDFs themselves fillable (AcroForm). Content is extracted to data.
- Not changing the Annex 1 PDF renderer in this work (separate concern).

## Architecture

Three pieces:

### 1. SAIC checklist catalog (the real data)

- Extract each PDF's items into structured JSON, keyed by SAIC number.
- Each entry: `{ saicNumber, title, sections: [{ no, title, items: [{ itemNo,
  acceptanceCriteria, referenceStandard }] }] }`.
- **Location:** backend seed resource (e.g. `Infrastructure/BlueSticker/SaicChecklists/*.json`),
  served via a read API `GET /saic-checklists/{saicNumber}`. Keeps content versioned, reusable
  for PDF rendering later, and out of the JS bundle.
- The existing `ChecklistItem` shape is the per-item target: `itemNo`, `acceptanceCriteria`,
  `referenceStandard`, `result` (defaults `NotSet`), `remark` (empty). Section headers render
  as non-scored group rows.

### 2. Equipment Type → SAIC mapping (per type, not per category)

Authoritative source: `Aramco Category & Equipment Types  .xlsx`. Keyed by **(Aramco category,
equipment type name)**. Full table:

| Category | Equipment Type | Inspection | Load Test |
|---|---|---|---|
| CR01 | Mobile Crane - All Terrain / Rough Terrain / Truck Mounted / Boom Truck / Crawler | 7007 | 7001 |
| CR02 | Elevator | 7005 | — |
| CR02 | Escalator | 7006 | — |
| CR03 | Manlift - Boom Supported EWP | 7013 | — |
| CR03 | Scissor Lift - Self Propelled EWP | 7014 | — |
| CR03 | Manually Propelled EWP | 7015 | — |
| CR03 | Mast Climbing Personal Platform | 7015 | — |
| CR04 | Pedestal Crane | 7018 | — |
| CR04 | Pedestal Crane - Articulating Boom | 7004 | — |
| CR04 | Floating Crane - Articulating Boom | 7004 | — |
| CR04 | Floating Crane | 7009 | — |
| CR04 | Overhead Crane | 7008 | — |
| CR04 | Monorail Crane | 7011 | — |
| CR04 | Tower Crane | 7003 | — |
| CR04 | Portal Crane | 7018 | — |
| CR05 | Storage Retrieval Machine (SRM) | 7012 | — |
| CR06 | Articulating Boom Crane | 7004 | — |
| CR07 | Lifting Beam | 7002 | — |
| CR07 | Spreader Beam | 7002 | — |
| CR08 | Powered Platform / Sky Climber | 7016 | — |
| CR09 | Bucket Truck | 7013 | — |
| CR10 | Manbasket | 7017 | — |
| CR11 | Overhead Crane | 7008 | — |
| CR11 | Monorail Crane | 7011 | — |
| CR11 | Jib Crane | 7011 | — |
| CR12 | Side Boom Tractor | 7010 | — |
| CR13 | A-frame | 7011 | — |
| CR13 | Gantry Crane | 7011 | — |
| CR14 | Tower Crane | 7003 | — |

- **Load Test (7001):** the Excel marks the Load-Test column on CR01 (merged). 7001 is the
  generic "Load Test Report" — already surfaced via the editor's "Load Test checklist" button.
  Treated as a single shared load-test checklist, applied where load testing is required.
- This replaces both category-level lookups in `AramcoCategory.cs` and `BlueStickerHandlers.cs`.
  Keying on (category, type name) is safe: duplicate type names across categories (Overhead,
  Monorail, Tower) resolve to the same SAIC number anyway.

### 3. Form wiring

- On equipment-type selection, the report's resolved SAIC number is computed (backend, from the
  mapping), exposed on the report DTO (`InspectionChecklistNumber`, already present).
- The checklist editor gains a "Load SAIC-U-70## checklist" action that fetches the catalog
  entry by number and populates the items (replacing the generic regex templates as the primary
  path; generic templates kept only as fallback when no SAIC entry exists).
- Filled checklist persists as today via `ChecklistJson`.

## Data Flow

```
pick equipment type
  → backend resolves (category, typeName) → SAIC number  [mapping #2]
  → report DTO.InspectionChecklistNumber
  → editor fetches GET /saic-checklists/{number}          [catalog #1]
  → items load into editor (result=NotSet)
  → inspector fills Pass/Fail/NA + remark
  → save → ChecklistJson on report
```

## Error Handling

- Equipment type with no SAIC mapping → editor falls back to generic template + warning banner.
- SAIC number with no catalog entry yet (during rollout) → editor shows "checklist not yet
  available for SAIC-U-70##" and allows manual rows; never blocks the inspection.
- Catalog fetch failure → editor stays usable with manual add-row.

## Testing

- **Mapping:** unit test every (category, type) row → expected SAIC number; assert no equipment
  type in the seeded `EquipmentType` set is left unmapped.
- **Catalog parse:** snapshot test — item count per SAIC matches the PDF (e.g. 7007 = 92).
- **Editor load:** loading a catalog entry populates N items with empty results.
- **Persistence:** fill + save + reload round-trips the checklist.

## Rollout

- **Pilot:** SAIC-U-7007 (Mobile Crane, CR01) end-to-end — extract → catalog → mapping → editor
  load → fill → save. Verify extracted items against the PDF.
- **Then:** extract the remaining 17 in a batch, each verified by item-count snapshot.

## Risks

- **PDF extraction fidelity.** `pdftotext -layout` splits multi-line acceptance criteria and
  puts the reference standard on its own line. ~700 items total. Extraction needs a verification
  pass (mandatory for the pilot; item-count snapshot + spot-check for the rest). A wrong/missing
  acceptance criterion is a compliance risk, so verification is part of the work, not optional.
- **Equipment type name drift.** Mapping keys on exact type names; if seeded `EquipmentType.Name`
  differs from the Excel wording, the join misses. The "no unmapped type" test catches this.
