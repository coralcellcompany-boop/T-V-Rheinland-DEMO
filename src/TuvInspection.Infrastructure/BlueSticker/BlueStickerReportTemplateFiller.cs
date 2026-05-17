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

    public byte[] Fill(BlueStickerReportDetailDto r)
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
}
