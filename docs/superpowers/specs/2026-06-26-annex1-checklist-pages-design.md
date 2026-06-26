# Annex 1 — Append SAIC Checklist Pages — Design Spec

**Date:** 2026-06-26
**Status:** Approved for planning
**Approach:** Render the checklist inline in both PDF paths (docx + QuestPDF fallback). No persistence, no new dependencies, no PDF merge.

## 1. Goal

The Blue Sticker **Annex 1** PDF (`{ReportNo}-Annex1.pdf`) currently renders only the
inspection report form. It must also append the applicable **SAIC-U-70## checklist** as
additional page(s) **after** the report form, so the downloaded document is the report
followed by its reference checklist.

The appended checklist is a **blank reference checklist**: the official SAIC items for the
equipment type, with **empty Result and Remark columns**. The inspector's per-item answers
are deliberately **not** captured or rendered (see §6).

## 2. Source of the checklist

- Each report already derives its checklist number into the DTO field
  `BlueStickerReportDetailDto.InspectionChecklistNumber` (e.g. `SAIC-U-7007` for a Mobile
  Crane), via `SaicChecklistMap.Resolve(...)` with the category fallback.
- The checklist items are loaded from the existing embedded catalog
  `SaicChecklistCatalog.Get(saicNumber)` → `SaicChecklistDto` (flattened items carrying
  `ItemNo`, `AcceptanceCriteria`, `ReferenceStandard`, `SectionNo`, `SectionTitle`).
- **No new data, no DB columns, no migration.** This is reference data already embedded as
  JSON resources.

## 3. Rendering — both paths stay consistent

Gotenberg is deployed (docker-compose + production), so the **docx → Gotenberg** path is the
real one; QuestPDF is the fallback when Gotenberg is unreachable. Both must produce the same
combined output.

### 3a. Primary path (docx + Gotenberg)
- `BlueStickerReportTemplateFiller.Fill(report, checklist)` gains a checklist parameter.
- After filling the existing Annex 1 table, when a checklist is present it appends to the
  document body (inserted **before** the body-level `sectPr`, so it inherits the same
  landscape section):
  1. A **page break** so the checklist starts on a fresh page.
  2. A **heading** paragraph: `Inspection Checklist — {SaicNumber} — {Title}`.
  3. A **table**: header row + one row per item, grouped by section with a section sub-header
     row before each section's items.
- Gotenberg then renders the report + checklist as **one** PDF (single conversion, no merge).

### 3b. Fallback path (QuestPDF)
- `RenderFallback(report, checklist)` keeps the existing report page, then, when a checklist
  is present, adds the checklist as **additional page(s)** in the same `Document.Create`.
- Reuse the table style already established by `CertificatePdfRenderer.ChecklistTable`
  (5 columns, repeating header). QuestPDF auto-paginates a long table across pages.

### 3c. Table layout (both paths)
| Column | Content |
|--------|---------|
| # | `ItemNo` (e.g. `1.1`) |
| Acceptance criteria | `AcceptanceCriteria` |
| Reference | `ReferenceStandard` |
| Result | **blank** cell |
| Remark | **blank** cell |

- Items grouped by `SectionNo` / `SectionTitle` with a section header row.
- Header row repeats on each page (Word `tblHeader`; QuestPDF `Table.Header`).
- Orientation: landscape, matching the Annex 1 sheet.

## 4. Wiring

- `BlueStickerReportPdfRenderer` gains a `SaicChecklistCatalog` dependency (already
  DI-registered; used by the resolve handler).
- In `RenderAsync`: resolve `checklist = InspectionChecklistNumber is {} n ? catalog.Get(n) : null;`
  then pass `checklist` to both `_filler.Fill(...)` and `RenderFallback(...)`.

## 5. Graceful edges

- `InspectionChecklistNumber` is null (unmapped category) → **no** checklist pages; report
  prints exactly as today.
- `catalog.Get(n)` returns null (number not embedded) → **no** checklist pages; report prints
  as today. No empty/heading-only pages.

## 6. Out of scope

- **No inspector answers.** Per-item Pass/Fail/NA + remarks are not stored for Blue Sticker
  reports today and remain unstored. Result/Remark columns are rendered blank.
- No changes to the report form layout, the workflow, the domain entity, EF config, or any
  API contract. `InspectionChecklistNumber` already exists on the DTO.
- No PDF merge library, no second Gotenberg call, no new NuGet package.
- The TPI `InspectionCertificate` checklist flow is untouched.

## 7. Testing

- **Filler (docx):** `Fill(report, checklist)` produces a docx whose body contains the
  checklist heading, the SAIC number, and the item text (e.g. an `ItemNo` /
  `AcceptanceCriteria` string) in a second table. Assert by reading the OpenXML body.
- **Filler null-safety:** `Fill(report, null)` adds **no** extra table — body has exactly the
  original single Annex 1 table.
- **Renderer resolve:** with a mapped `InspectionChecklistNumber`, the renderer requests that
  number from the catalog; with null, it does not append.
- Existing Blue Sticker / SAIC tests continue to pass.

## 8. Open items (resolve during planning)

- Confirm `SaicChecklistCatalog` is resolvable from DI for injection into the renderer
  (Scrutor scan); if not, register it.
- Confirm the body-level `sectPr` insertion point in `Annex1.docx` so appended content keeps
  the intended orientation and doesn't disturb the existing table's section.
