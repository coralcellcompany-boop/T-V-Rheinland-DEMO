using System.Reflection;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using A = DocumentFormat.OpenXml.Drawing;
using DW = DocumentFormat.OpenXml.Drawing.Wordprocessing;
using PIC = DocumentFormat.OpenXml.Drawing.Pictures;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>
/// Fills the embedded official Annex 1 (MS0053813) docx with BlueStickerReport values.
/// Reuses the same template + positional row map as Annex1TemplateFiller; adds the three
/// signature images. Output is docx bytes for Gotenberg.
/// </summary>
public sealed class BlueStickerReportTemplateFiller
{
    private const string TemplateResourceName =
        "TuvInspection.Infrastructure.Certificates.Templates.Annex1.docx";

    public byte[] Fill(BlueStickerReportDetailDto r, SaicChecklistDto? checklist = null)
    {
        using var output = new MemoryStream();
        using (var template = Assembly.GetExecutingAssembly()
                   .GetManifestResourceStream(TemplateResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{TemplateResourceName}' not found."))
        {
            template.CopyTo(output);
        }
        output.Position = 0;

        using (var doc = WordprocessingDocument.Open(output, isEditable: true))
        {
            var body = doc.MainDocumentPart!.Document.Body!;
            var table = body.Elements<Table>().FirstOrDefault()
                ?? throw new InvalidOperationException("Annex 1 template has no table.");
            var rows = table.Elements<TableRow>().ToList();

            // Row index map mirrors Annex1TemplateFiller exactly (same docx template):
            //   2  → TUV JO / Aramco Cat / Org Code / RPO / CRM / Report No
            //   4  → Department / Date / Time / Prev Sticker / Issued By
            //   6  → Area / Equip ID / Capacity / Location / Result / New Sticker
            //   8  → Manufacturer / Model / Equip Type / Serial / Sticker Expiry
            //   11 → Deficiencies / Corrective Actions
            //   14 → Receiver / Inspector / Tech Reviewer names, badge, telephone, dates
            //   15 → Signature images (3 cells: Receiver / Inspector / Tech Reviewer)
            FillRowCells(rows[2], r.TuvJobOrderNo, r.AramcoCategoryNo, r.OrgCode, r.RpoNo,
                r.CrmNo, r.ReportNo);
            FillRowCells(rows[4], r.DepartmentContractor,
                r.InspectionDate?.ToString("dd MMM yyyy"),
                r.InspectionTime?.ToString("HH:mm"),
                r.PreviousStickerNo, r.PreviousStickerIssuedBy);
            FillRowCells(rows[6], r.AreaOfInspection, r.EquipmentIdNo, r.Capacity,
                r.EquipmentLocation, ResultLabel(r.Result), r.NewStickerNo);
            FillRowCells(rows[8], r.Manufacturer, r.Model, r.EquipmentType,
                r.EquipmentSerialNo, r.StickerExpirationDate?.ToString("dd MMM yyyy"));
            FillRowCells(rows[11], r.Deficiencies, r.CorrectiveActionsTaken);
            FillRowCells(rows[14], r.ReceiverName, r.ReceiverBadgeNo, r.ReceiverTelephone,
                r.InspectorName, r.InspectorSapNo, r.InspectorTelephone,
                r.TechnicalReviewerName, r.ReceivedDate?.ToString("dd MMM yyyy"),
                r.ReviewedDate?.ToString("dd MMM yyyy"));

            // Signature row (row 15) — 3 cells: Receiver / Inspector / Technical Reviewer
            var sigCells = rows[15].Elements<TableCell>().ToList();
            PlaceSignature(doc, sigCells.ElementAtOrDefault(0), r.ReceiverSignaturePng, 1u);
            PlaceSignature(doc, sigCells.ElementAtOrDefault(1), r.InspectorSignaturePng, 2u);
            PlaceSignature(doc, sigCells.ElementAtOrDefault(2), r.TechnicalReviewerSignaturePng, 3u);

            if (checklist is { Items.Count: > 0 })
                AppendChecklist(body, checklist);

            doc.MainDocumentPart.Document.Save();
        }
        return output.ToArray();
    }

    private static void FillRowCells(TableRow row, params string?[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (var i = 0; i < values.Length && i < cells.Count; i++)
            SetCellText(cells[i], values[i]);
    }

    private static void SetCellText(TableCell cell, string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim();
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
        if (paragraph is null) { paragraph = new Paragraph(); cell.Append(paragraph); }
        var existingRun = paragraph.Elements<Run>().FirstOrDefault();
        var rPr = existingRun?.RunProperties?.CloneNode(true) as RunProperties;
        foreach (var run in paragraph.Elements<Run>().ToList()) run.Remove();
        foreach (var extra in cell.Elements<Paragraph>().Skip(1).ToList()) extra.Remove();
        var newRun = new Run();
        if (rPr is not null) newRun.AppendChild(rPr);
        newRun.AppendChild(new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        paragraph.AppendChild(newRun);
    }

    private static void PlaceSignature(WordprocessingDocument doc, TableCell? cell, string? dataUrl, uint drawingId)
    {
        if (cell is null || string.IsNullOrWhiteSpace(dataUrl)) return;
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return;
        byte[] png;
        try { png = Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { return; }

        var part = doc.MainDocumentPart!.AddImagePart(ImagePartType.Png);
        using (var s = new MemoryStream(png)) part.FeedData(s);
        var relId = doc.MainDocumentPart!.GetIdOfPart(part);

        long cx = 1200000, cy = 400000; // ~1.25in x 0.42in EMUs — fits the signature cell
        var drawing = new Drawing(
            new DW.Inline(
                new DW.Extent { Cx = cx, Cy = cy },
                new DW.EffectExtent { LeftEdge = 0L, TopEdge = 0L, RightEdge = 0L, BottomEdge = 0L },
                new DW.DocProperties { Id = (UInt32Value)drawingId, Name = "sig" },
                new A.Graphic(new A.GraphicData(
                    new PIC.Picture(
                        new PIC.NonVisualPictureProperties(
                            new PIC.NonVisualDrawingProperties { Id = drawingId, Name = "sig.png" },
                            new PIC.NonVisualPictureDrawingProperties()),
                        new PIC.BlipFill(
                            new A.Blip { Embed = relId },
                            new A.Stretch(new A.FillRectangle())),
                        new PIC.ShapeProperties(
                            new A.Transform2D(
                                new A.Offset { X = 0L, Y = 0L },
                                new A.Extents { Cx = cx, Cy = cy }),
                            new A.PresetGeometry(new A.AdjustValueList())
                                { Preset = A.ShapeTypeValues.Rectangle })))
                    { Uri = "http://schemas.openxmlformats.org/drawingml/2006/picture" }))
            { DistanceFromTop = 0U, DistanceFromBottom = 0U,
              DistanceFromLeft = 0U, DistanceFromRight = 0U });

        var para = cell.Elements<Paragraph>().FirstOrDefault();
        if (para is null) { para = new Paragraph(); cell.Append(para); }
        foreach (var rn in para.Elements<Run>().ToList()) rn.Remove();
        para.AppendChild(new Run(drawing));
    }

    private static string ResultLabel(BlueStickerResultDto r) => r switch
    {
        BlueStickerResultDto.Pass => "PASS",
        BlueStickerResultDto.Fail => "FAIL",
        _ => "—",
    };

    // Checklist column widths in twips (landscape A4 ≈ 14400 usable). #, Criteria, Reference, Result, Remark.
    private static readonly int[] ChecklistCols = { 720, 5760, 3600, 1440, 2880 };
    private static readonly string ChecklistTableWidthTwips = ChecklistCols.Sum().ToString();

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
                new TableWidth { Width = ChecklistTableWidthTwips, Type = TableWidthUnitValues.Dxa },
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
        var runProps = bold
            ? new RunProperties(new Bold(), new FontSize { Val = "16" })   // 8pt
            : new RunProperties(new FontSize { Val = "16" });              // 8pt

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
                new TableCellWidth { Width = ChecklistTableWidthTwips, Type = TableWidthUnitValues.Dxa },
                new GridSpan { Val = 5 },
                new Shading { Val = ShadingPatternValues.Clear, Color = "auto", Fill = "EFEFEF" }),
            paragraph);

        return new TableRow(cell);
    }
}
