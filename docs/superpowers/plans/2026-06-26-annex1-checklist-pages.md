# Annex 1 — Append SAIC Checklist Pages — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Append the applicable SAIC-U-70## reference checklist (blank Result/Remark columns) as extra page(s) after the Blue Sticker Annex 1 report, in both the docx→Gotenberg path and the QuestPDF fallback.

**Architecture:** The report DTO already carries `InspectionChecklistNumber`. The renderer resolves the checklist from the existing `SaicChecklistCatalog` and passes it to (a) `BlueStickerReportTemplateFiller`, which appends an OpenXML page-break + heading + table into the docx before Gotenberg conversion, and (b) the QuestPDF fallback, which adds extra pages with the same table. No DB changes, no persistence, no PDF-merge library, no new NuGet package.

**Tech Stack:** .NET 10, DocumentFormat.OpenXml 3.3.0 (docx), QuestPDF 2026.2.4 (fallback), xUnit + FluentAssertions (tests). Spec: `docs/superpowers/specs/2026-06-26-annex1-checklist-pages-design.md`.

---

## Conventions

**Run all dotnet commands with the SDK exported first** (SDK is not on PATH):
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
```

Unit test project: `tests/TuvInspection.UnitTests` (xUnit; `using Xunit` is global). Run a single class with:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~<ClassName>"
```

---

## File Structure

- **Modify** `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportTemplateFiller.cs` — add a `SaicChecklistDto? checklist` param to `Fill` and the OpenXML checklist append.
- **Modify** `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportPdfRenderer.cs` — inject `SaicChecklistCatalog`, resolve the checklist, pass it to the filler and the fallback; add a QuestPDF checklist table; make the fallback `internal static` for testing.
- **Modify** `src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs` — register `SaicChecklistCatalog`.
- **Modify** `src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj` — `InternalsVisibleTo` the unit-test assembly.
- **Create** `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerTestData.cs` — shared sample `BlueStickerReportDetailDto` builder.
- **Create** `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportTemplateFillerTests.cs` — docx append tests.
- **Create** `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportPdfRendererTests.cs` — QuestPDF fallback smoke test.

---

## Task 1: Shared test data builder

The `BlueStickerReportDetailDto` is a 44-field positional record with no test factory. Create one shared builder so both test classes (Tasks 2 & 3) stay DRY.

**Files:**
- Create: `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerTestData.cs`

- [ ] **Step 1: Create the builder**

```csharp
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

/// <summary>Shared minimal <see cref="BlueStickerReportDetailDto"/> for renderer/filler tests.
/// Only the fields the PDF reads are meaningful; everything else is null/default.</summary>
internal static class BlueStickerTestData
{
    public static BlueStickerReportDetailDto SampleReport(string? checklistNumber = "SAIC-U-7007") => new(
        Id: Guid.Empty,
        ReportNo: "IS-NA-2026-003",
        JobOrderId: Guid.Empty,
        EquipmentId: Guid.Empty,
        ClientId: Guid.Empty,
        TuvJobOrderNo: "JO-1",
        AramcoCategoryNo: "CR01",
        OrgCode: null, RpoNo: null, CrmNo: null, DepartmentContractor: null,
        InspectionDate: null, InspectionTime: null,
        PreviousStickerNo: null, PreviousStickerIssuedBy: null,
        AreaOfInspection: null,
        Result: BlueStickerResultDto.Pass,
        EquipmentIdNo: "DEV-YANBU-EQ-001",
        Capacity: null, EquipmentLocation: null,
        Manufacturer: null, Model: null,
        EquipmentType: "Mobile Crane (Telescopic Boom)",
        EquipmentSerialNo: null, NewStickerNo: null,
        StickerExpirationDate: null,
        Deficiencies: null, CorrectiveActionsTaken: null,
        ReceiverName: null, ReceiverBadgeNo: null, ReceiverTelephone: null,
        InspectorName: null, InspectorSapNo: null, InspectorTelephone: null,
        TechnicalReviewerName: null,
        ReceivedDate: null, ReviewedDate: null,
        ReceiverSignaturePng: null, InspectorSignaturePng: null, TechnicalReviewerSignaturePng: null,
        State: BlueStickerReportStateDto.ClientSigned,
        CreatedAtUtc: default,
        UpdatedAtUtc: null,
        Transitions: Array.Empty<BlueStickerTransitionDto>(),
        InspectionChecklistNumber: checklistNumber);
}
```

- [ ] **Step 2: Verify it compiles**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet build tests/TuvInspection.UnitTests
```
Expected: Build succeeded. (If a field name/order mismatch errors, reconcile against `src/TuvInspection.Contracts/BlueSticker/BlueStickerDtos.cs` `BlueStickerReportDetailDto`.)

- [ ] **Step 3: Commit**

```bash
git add tests/TuvInspection.UnitTests/BlueSticker/BlueStickerTestData.cs
git commit -m "test(saic): shared Blue Sticker report DTO builder for PDF tests

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 2: Append the checklist into the docx (filler)

**Files:**
- Modify: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportTemplateFiller.cs`
- Test: `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportTemplateFillerTests.cs`

- [ ] **Step 1: Write the failing tests**

Create `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportTemplateFillerTests.cs`:

```csharp
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportTemplateFillerTests
{
    [Fact]
    public void Fill_with_checklist_appends_second_table_with_items_and_heading()
    {
        var checklist = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(checklist);

        var bytes = new BlueStickerReportTemplateFiller()
            .Fill(BlueStickerTestData.SampleReport(), checklist);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart!.Document.Body!;
        var tables = body.Elements<Table>().ToList();

        Assert.Equal(2, tables.Count);                                  // original Annex 1 + checklist
        Assert.Contains("SAIC-U-7007", body.InnerText);                 // heading text
        Assert.Contains(checklist!.Items[0].ItemNo, tables[1].InnerText);
        Assert.Contains(checklist.Items[0].AcceptanceCriteria, tables[1].InnerText);
    }

    [Fact]
    public void Fill_without_checklist_adds_no_extra_table()
    {
        var bytes = new BlueStickerReportTemplateFiller()
            .Fill(BlueStickerTestData.SampleReport(checklistNumber: null), checklist: null);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var tables = doc.MainDocumentPart!.Document.Body!.Elements<Table>().ToList();

        Assert.Single(tables);
    }
}
```

- [ ] **Step 2: Run tests to verify they fail**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~BlueStickerReportTemplateFillerTests"
```
Expected: compile error / FAIL — `Fill` does not yet accept a `checklist` argument.

- [ ] **Step 3: Add the `checklist` parameter and the append call**

In `BlueStickerReportTemplateFiller.cs`, change the method signature and insert the append before `Save()`.

Change the signature (line 22):
```csharp
    public byte[] Fill(BlueStickerReportDetailDto r, SaicChecklistDto? checklist = null)
```

Then, immediately before `doc.MainDocumentPart.Document.Save();` (currently line 71), insert:
```csharp
            if (checklist is { Items.Count: > 0 })
                AppendChecklist(body, checklist);

```

- [ ] **Step 4: Add the OpenXML builder helpers**

Add these members to the `BlueStickerReportTemplateFiller` class (e.g. after the `Fill` method). The column widths sum to 14400 twips to fit the Annex 1 landscape page.

```csharp
    // Checklist column widths in twips (landscape A4 ≈ 14400 usable). #, Criteria, Reference, Result, Remark.
    private static readonly int[] ChecklistCols = { 720, 5760, 3600, 1440, 2880 };

    /// <summary>Appends a page break, a heading and the SAIC checklist table to the body,
    /// keeping them inside the document's existing (landscape) section.</summary>
    private static void AppendChecklist(Body body, SaicChecklistDto checklist)
    {
        var sectPr = body.Elements<SectionProperties>().LastOrDefault();

        var pageBreak = new Paragraph(new Run(new Break { Type = BreakValues.Page }));

        var heading = new Paragraph(
            new ParagraphProperties(new SpacingBetweenLines { After = "120" }),
            new Run(
                new RunProperties(new Bold(), new FontSize { Val = "24" }),  // 12pt
                new Text($"Inspection Checklist — {checklist.SaicNumber} — {checklist.Title}")
                    { Space = SpaceProcessingModeValues.Preserve }));

        var table = BuildChecklistTable(checklist);

        if (sectPr is not null)
        {
            body.InsertBefore(pageBreak, sectPr);
            body.InsertBefore(heading, sectPr);
            body.InsertBefore(table, sectPr);
        }
        else
        {
            body.Append(pageBreak);
            body.Append(heading);
            body.Append(table);
        }
    }

    private static Table BuildChecklistTable(SaicChecklistDto checklist)
    {
        var table = new Table(
            new TableProperties(
                new TableWidth { Width = "14400", Type = TableWidthUnitValues.Dxa },
                new TableBorders(
                    new TopBorder { Val = BorderValues.Single, Size = 4U, Color = "999999" },
                    new LeftBorder { Val = BorderValues.Single, Size = 4U, Color = "999999" },
                    new BottomBorder { Val = BorderValues.Single, Size = 4U, Color = "999999" },
                    new RightBorder { Val = BorderValues.Single, Size = 4U, Color = "999999" },
                    new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4U, Color = "BBBBBB" },
                    new InsideVerticalBorder { Val = BorderValues.Single, Size = 4U, Color = "BBBBBB" }),
                new TableLayout { Type = TableLayoutValues.Fixed }),
            new TableGrid(ChecklistCols.Select(w => new GridColumn { Width = w.ToString() }).ToArray()));

        // Header row — repeats on every page (TableHeader).
        var header = new TableRow(new TableRowProperties(new TableHeader()));
        string[] heads = { "#", "Acceptance criteria", "Reference", "Result", "Remark" };
        for (var i = 0; i < heads.Length; i++)
            header.Append(MakeChecklistCell(heads[i], ChecklistCols[i], bold: true, shade: "D9D9D9"));
        table.Append(header);

        string? currentSection = null;
        foreach (var item in checklist.Items)
        {
            var sectionKey = $"{item.SectionNo} {item.SectionTitle}".Trim();
            if (sectionKey.Length > 0 && sectionKey != currentSection)
            {
                currentSection = sectionKey;
                table.Append(MakeSectionRow(sectionKey));
            }

            var row = new TableRow();
            row.Append(MakeChecklistCell(item.ItemNo, ChecklistCols[0], bold: false, shade: null));
            row.Append(MakeChecklistCell(item.AcceptanceCriteria, ChecklistCols[1], bold: false, shade: null));
            row.Append(MakeChecklistCell(item.ReferenceStandard, ChecklistCols[2], bold: false, shade: null));
            row.Append(MakeChecklistCell("", ChecklistCols[3], bold: false, shade: null));  // Result (blank)
            row.Append(MakeChecklistCell("", ChecklistCols[4], bold: false, shade: null));  // Remark (blank)
            table.Append(row);
        }

        return table;
    }

    private static TableCell MakeChecklistCell(string? text, int widthTwips, bool bold, string? shade)
    {
        var runProps = new RunProperties();
        if (bold) runProps.AppendChild(new Bold());
        runProps.AppendChild(new FontSize { Val = "16" });  // 8pt

        var paragraph = new Paragraph(
            new Run(runProps, new Text(text ?? string.Empty) { Space = SpaceProcessingModeValues.Preserve }));

        var cellProps = new TableCellProperties(
            new TableCellWidth { Width = widthTwips.ToString(), Type = TableWidthUnitValues.Dxa });
        if (shade is not null)
            cellProps.AppendChild(new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = shade });

        return new TableCell(cellProps, paragraph);
    }

    private static TableRow MakeSectionRow(string title)
    {
        var paragraph = new Paragraph(
            new Run(
                new RunProperties(new Bold(), new FontSize { Val = "16" }),
                new Text(title) { Space = SpaceProcessingModeValues.Preserve }));

        var cell = new TableCell(
            new TableCellProperties(
                new TableCellWidth { Width = "14400", Type = TableWidthUnitValues.Dxa },
                new GridSpan { Val = 5 },
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "EFEFEF" }),
            paragraph);

        return new TableRow(cell);
    }
```

Note: `SaicChecklistDto` lives in `TuvInspection.Contracts.BlueSticker`, already imported at the top of the file (`using TuvInspection.Contracts.BlueSticker;`). All OpenXML types used here (`Body`, `Table`, `TableRow`, `TableCell`, `Shading`, `GridSpan`, `Break`, etc.) are in `DocumentFormat.OpenXml.Wordprocessing`, already imported.

- [ ] **Step 5: Run tests to verify they pass**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~BlueStickerReportTemplateFillerTests"
```
Expected: PASS (2 passed).

- [ ] **Step 6: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportTemplateFiller.cs \
        tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportTemplateFillerTests.cs
git commit -m "feat(saic): append SAIC checklist pages into the Annex 1 docx

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 3: Wire the renderer + QuestPDF fallback + DI

**Files:**
- Modify: `src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportPdfRenderer.cs`
- Modify: `src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs`
- Modify: `src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj`
- Test: `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportPdfRendererTests.cs`

- [ ] **Step 1: Expose internals to the test assembly**

In `src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj`, add inside a `<Project>` `ItemGroup` (next to the other `ItemGroup`s):
```xml
  <ItemGroup>
    <InternalsVisibleTo Include="TuvInspection.UnitTests" />
  </ItemGroup>
```

- [ ] **Step 2: Write the failing fallback test**

Create `tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportPdfRendererTests.cs`:

```csharp
using System.Text;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportPdfRendererTests
{
    [Fact]
    public void Fallback_renders_valid_pdf_including_checklist_pages()
    {
        var checklist = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(checklist);

        var bytes = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(), checklist);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 3000, $"expected a multi-page PDF, got {bytes.Length} bytes");
    }

    [Fact]
    public void Fallback_without_checklist_still_renders_valid_pdf()
    {
        var bytes = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(checklistNumber: null), checklist: null);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
```

- [ ] **Step 3: Run the test to verify it fails**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~BlueStickerReportPdfRendererTests"
```
Expected: compile error / FAIL — `RenderFallback` is `private`, instance, and takes one argument.

- [ ] **Step 4: Inject the catalog and resolve the checklist**

In `BlueStickerReportPdfRenderer.cs`:

Add the field (next to the other fields, ~line 21):
```csharp
    private readonly SaicChecklistCatalog _catalog;
```

Replace the constructor (lines 28-30) with:
```csharp
    public BlueStickerReportPdfRenderer(BlueStickerReportTemplateFiller filler,
        GotenbergClient gotenberg, SaicChecklistCatalog catalog,
        ILogger<BlueStickerReportPdfRenderer> log)
    { _filler = filler; _gotenberg = gotenberg; _catalog = catalog; _log = log; }
```

Replace `RenderAsync` (lines 32-46) with:
```csharp
    public async Task<byte[]> RenderAsync(BlueStickerReportDetailDto r, CancellationToken ct = default)
    {
        var checklist = r.InspectionChecklistNumber is { Length: > 0 } n ? _catalog.Get(n) : null;
        try
        {
            var docx = _filler.Fill(r, checklist);
            return await _gotenberg.ConvertDocxToPdfAsync(docx, $"{r.ReportNo}-Annex1.docx", ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Blue Sticker Annex 1 docx→pdf via gotenberg failed for {Report}; using fallback.",
                r.ReportNo);
            return RenderFallback(r, checklist);
        }
    }
```

- [ ] **Step 5: Make the fallback `internal static` and add the checklist pages**

Change the `RenderFallback` signature (currently `private byte[] RenderFallback(BlueStickerReportDetailDto r) =>`) and wrap the existing single-page body so the checklist pages are added after it. Replace the opening of the method — from `private byte[] RenderFallback(...) =>` through the start of the page lambda — so it reads:

```csharp
    internal static byte[] RenderFallback(BlueStickerReportDetailDto r, SaicChecklistDto? checklist) =>
        Document.Create(c =>
        {
            c.Page(p =>
            {
                p.Size(PageSizes.A4.Landscape());
                p.Margin(20);
                p.Content().Table(t =>
                {
```

…keep the entire existing table body unchanged… and then **after** the existing `p.Content().Table(t => { ... });` page closes (i.e. replacing the old `})).GeneratePdf();` tail), close the first page and add the checklist page:

```csharp
                });   // end p.Content().Table
            });       // end first c.Page

            if (checklist is { Items.Count: > 0 })
                c.Page(p =>
                {
                    p.Size(PageSizes.A4.Landscape());
                    p.Margin(20);
                    p.Content().Column(col =>
                    {
                        col.Item().PaddingBottom(8)
                            .Text($"Inspection Checklist — {checklist.SaicNumber} — {checklist.Title}")
                            .FontSize(12).Bold();
                        col.Item().Element(box => ChecklistTable(box, checklist));
                    });
                });
        }).GeneratePdf();
```

> Implementation note: this converts the expression-bodied `=>` single-`Page` lambda into a `Document.Create(c => { ... })` block with one or two `c.Page(...)` calls. The existing main-page table content (all the `L`/`D`/`BH`/`SignatureCell` calls) is moved verbatim inside the first `c.Page`. Because the method no longer uses instance state, it is `static`; the static helpers it calls (`SignatureCell`, `DecodePng`) are already static.

- [ ] **Step 6: Add the QuestPDF checklist table helper**

Add this method to `BlueStickerReportPdfRenderer` (e.g. after `RenderFallback`):

```csharp
    private static void ChecklistTable(IContainer container, SaicChecklistDto checklist)
    {
        container.Border(0.5f).BorderColor("#cccccc").Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                cd.ConstantColumn(30);   // #
                cd.RelativeColumn(5);    // Acceptance criteria
                cd.RelativeColumn(3);    // Reference
                cd.ConstantColumn(60);   // Result (blank)
                cd.RelativeColumn(3);    // Remark (blank)
            });

            t.Header(h =>
            {
                void HC(string s) => h.Cell().Background("#D9D9D9").Padding(4).Text(s).Bold().FontSize(8);
                HC("#"); HC("Acceptance criteria"); HC("Reference"); HC("Result"); HC("Remark");
            });

            string? section = null;
            foreach (var i in checklist.Items)
            {
                var key = $"{i.SectionNo} {i.SectionTitle}".Trim();
                if (key.Length > 0 && key != section)
                {
                    section = key;
                    t.Cell().ColumnSpan(5).Background("#EFEFEF").Padding(3).Text(key).Bold().FontSize(8);
                }

                t.Cell().Padding(3).Text(i.ItemNo).FontSize(8);
                t.Cell().Padding(3).Text(i.AcceptanceCriteria).FontSize(8);
                t.Cell().Padding(3).Text(i.ReferenceStandard).FontSize(8);
                t.Cell().Padding(3).Text(" ").FontSize(8);   // Result (blank)
                t.Cell().Padding(3).Text(" ").FontSize(8);   // Remark (blank)
            }
        });
    }
```

`IContainer` is in `QuestPDF.Infrastructure` (already imported). `SaicChecklistDto` is in `TuvInspection.Contracts.BlueSticker` (already imported).

- [ ] **Step 7: Register the catalog in DI**

In `src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs`, next to the other Blue Sticker registrations (after the `BlueStickerReportPdfRenderer` registration, ~line 151), add:
```csharp
        services.AddSingleton<TuvInspection.Infrastructure.BlueSticker.SaicChecklistCatalog>();
```

- [ ] **Step 8: Run the renderer tests**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/TuvInspection.UnitTests --filter "FullyQualifiedName~BlueStickerReportPdfRendererTests"
```
Expected: PASS (2 passed).

- [ ] **Step 9: Commit**

```bash
git add src/TuvInspection.Infrastructure/BlueSticker/BlueStickerReportPdfRenderer.cs \
        src/TuvInspection.Infrastructure/DependencyInjection/InfrastructureModule.cs \
        src/TuvInspection.Infrastructure/TuvInspection.Infrastructure.csproj \
        tests/TuvInspection.UnitTests/BlueSticker/BlueStickerReportPdfRendererTests.cs
git commit -m "feat(saic): render checklist pages in Annex 1 PDF (gotenberg + fallback)

Co-Authored-By: Claude Opus 4.8 (1M context) <noreply@anthropic.com>"
```

---

## Task 4: Full verification

**Files:** none (verification only).

- [ ] **Step 1: Build the whole solution**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet build src/TuvInspection.Api
```
Expected: Build succeeded, 0 errors.

- [ ] **Step 2: Run the full unit-test suite (no regressions)**

Run:
```bash
export DOTNET_ROOT="$HOME/.dotnet"; export PATH="$HOME/.dotnet:$PATH"
dotnet test tests/TuvInspection.UnitTests
```
Expected: all pass, including the existing `SaicChecklist*` and `BlueSticker*` tests.

- [ ] **Step 3: Manual PDF spot-check (real Gotenberg path)**

Bring up Gotenberg if not running (`docker compose up -d gotenberg`), run the API, and download a signed Blue Sticker report PDF:
```
GET /api/blue-sticker-reports/{id}/report.pdf?draft=true
```
Confirm: page 1 is the inspection form; page 2+ is the SAIC-U-7007 checklist with the items listed and **empty** Result/Remark columns. Confirm a report whose category does not map (no `InspectionChecklistNumber`) still renders with **no** trailing checklist pages.

- [ ] **Step 4: Finalize the branch**

Use the superpowers:finishing-a-development-branch skill to merge `feat/annex1-checklist-pages` into `main` (or open a PR), per the user's preference.

---

## Self-Review notes (resolved)

- **Spec coverage:** blank checklist (Task 2/3 render blank Result/Remark), both paths (Task 2 docx, Task 3 fallback), source = catalog via `InspectionChecklistNumber` (Task 3 Step 4), graceful null edges (Task 2 "no extra table" test + Task 3 "without checklist" test + Task 4 Step 3 manual), DI (Task 3 Step 7), tests (Tasks 2 & 3). ✔
- **Type consistency:** `Fill(BlueStickerReportDetailDto, SaicChecklistDto?)`, `RenderFallback(BlueStickerReportDetailDto, SaicChecklistDto?)`, `ChecklistTable(IContainer, SaicChecklistDto)`, `SaicChecklistCatalog.Get(string)→SaicChecklistDto?` are used consistently across tasks. ✔
- **OpenXML element ordering** (schema-sensitive): `TableProperties` = tblW → tblBorders → tblLayout; `TableBorders` = top → left → bottom → right → insideH → insideV; `RunProperties` = b → sz; `TableCellProperties` = tcW → gridSpan → shd. Encoded as written above. ✔
