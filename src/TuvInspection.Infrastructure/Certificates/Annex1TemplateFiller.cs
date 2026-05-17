using System.Reflection;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Infrastructure.Certificates;

/// <summary>
/// Fills the embedded official Aramco Annex 1 (MS0053813) docx template with values
/// from a certificate. The template structure is fixed (11-column, 16-row table) so we
/// fill cells positionally rather than by label-matching.
///
/// The output is the modified docx as a byte array, ready to be POSTed to gotenberg
/// for docx→pdf conversion. This is the only path that gives byte-for-byte fidelity
/// with the original Word layout.
/// </summary>
public sealed class Annex1TemplateFiller
{
    private const string TemplateResourceName =
        "TuvInspection.Infrastructure.Certificates.Templates.Annex1.docx";

    /// <summary>
    /// Returns the embedded template populated with the given values. The caller is
    /// responsible for converting the docx bytes to PDF (via gotenberg).
    /// </summary>
    public byte[] Fill(CertificateDetailDto cert, AramcoReportData data,
        string inspectorName, string? inspectorSapNo, string? equipmentSwl)
    {
        // Copy the embedded template to a writable memory stream.
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
            // Row indexes match the extracted layout exactly:
            //   0  title
            //   1  labels (TUV JO / Aramco Cat / Org Code / RPO / CRM / Report)
            //   2  data
            //   3  labels (Department / Inspection Date / Time / Prev Sticker / Issued By)
            //   4  data
            //   5  labels (Area / Equip ID / Capacity / Location / Result / New Sticker)
            //   6  data
            //   7  labels (Manufacturer / Model / Equip Type / Serial / Sticker Expiry)
            //   8  data
            //   9  compliance paragraph
            //   10 deficiencies / corrective actions header
            //   11 deficiencies / corrective actions data
            //   12 RECIEVER / INSPECTOR / TECHNICAL REVIEWER header
            //   13 signature labels
            //   14 signature data
            //   15 "Signature" placeholder
            FillRowCells(rows[2],
                data.TuvJobOrderNo,
                data.AramcoCategoryNo ?? cert.EquipmentAramcoCategory,
                data.OrgCode,
                data.RpoNo,
                data.CrmNo,
                data.ReportNo ?? cert.CertificateNo);

            FillRowCells(rows[4],
                data.DepartmentContractor,
                cert.InspectionDate.ToString("dd MMM yyyy"),
                data.InspectionTime?.ToString("HH:mm"),
                data.PreviousStickerNo,
                data.PreviousStickerIssuedBy);

            FillRowCells(rows[6],
                data.AreaOfInspection,
                cert.EquipmentIdNo,
                data.Capacity ?? equipmentSwl,
                data.EquipmentLocationOnSite,
                ResultLabel(cert.Result),
                cert.StickerNo);

            FillRowCells(rows[8],
                data.Manufacturer,
                data.Model,
                data.EquipmentType ?? cert.EquipmentTypeName,
                data.EquipmentSerialNo,
                (data.StickerExpirationDate ?? cert.NextDueDate)?.ToString("dd MMM yyyy"));

            // Build deficiency / corrective-action text from structured items when present;
            // fall back to the legacy free-text scalars for older certificates.
            string? deficienciesText;
            string? correctiveText;
            if (data.DeficiencyItems is { Count: > 0 })
            {
                var defLines = new System.Text.StringBuilder();
                var corrLines = new System.Text.StringBuilder();
                var idx = 0;
                foreach (var item in data.DeficiencyItems)
                {
                    idx++;
                    var prefix = $"{idx}. ";
                    var codePrefix = string.IsNullOrWhiteSpace(item.Code) ? string.Empty : $"[{item.Code}] ";
                    var desc = string.IsNullOrWhiteSpace(item.Description)
                        ? string.Empty
                        : $"{prefix}{codePrefix}{item.Description}";
                    var corr = string.IsNullOrWhiteSpace(item.CorrectiveAction)
                        ? string.Empty
                        : $"{prefix}{item.CorrectiveAction}";

                    if (defLines.Length > 0) defLines.Append('\n');
                    defLines.Append(desc);

                    if (corrLines.Length > 0) corrLines.Append('\n');
                    corrLines.Append(corr);
                }
                deficienciesText = defLines.ToString();
                correctiveText = corrLines.ToString();
            }
            else
            {
                // Legacy fallback: older certs that still have free-text fields.
                deficienciesText = data.Deficiencies;
                correctiveText = data.CorrectiveActionsTaken;
            }

            FillRowCells(rows[11], deficienciesText, correctiveText);

            // Row 14 — signatures data row (9 cells)
            FillRowCells(rows[14],
                data.ReceiverName,
                data.ReceiverBadgeNo,
                data.ReceiverTelephone,
                inspectorName,
                inspectorSapNo,
                data.InspectorTelephone,
                null,                                                 // tech reviewer name (post-approval)
                data.ReceivedDate?.ToString("dd MMM yyyy"),
                data.ReviewedDate?.ToString("dd MMM yyyy"));

            doc.MainDocumentPart.Document.Save();
        }

        return output.ToArray();
    }

    /// <summary>
    /// Replaces the text of each cell in a row positionally. Cells that already have
    /// content keep their formatting (run properties from the first run); the text in
    /// every existing <c>w:t</c> is removed and a single new run carrying the value is
    /// appended to the first paragraph. Cells past the supplied <paramref name="values"/>
    /// length are left untouched.
    /// </summary>
    private static void FillRowCells(TableRow row, params string?[] values)
    {
        var cells = row.Elements<TableCell>().ToList();
        for (var i = 0; i < values.Length && i < cells.Count; i++)
        {
            SetCellText(cells[i], values[i]);
        }
    }

    private static void SetCellText(TableCell cell, string? value)
    {
        var text = string.IsNullOrWhiteSpace(value) ? string.Empty : value!.Trim();

        // Use the first paragraph as our target; preserve its existing pPr alignment.
        var paragraph = cell.Elements<Paragraph>().FirstOrDefault();
        if (paragraph is null)
        {
            paragraph = new Paragraph();
            cell.Append(paragraph);
        }

        // Capture any existing run properties (font / size / colour) so the inserted
        // value inherits the template's styling.
        var existingRun = paragraph.Elements<Run>().FirstOrDefault();
        var rPr = existingRun?.RunProperties?.CloneNode(true) as RunProperties;

        // Drop existing runs (we are replacing the cell value).
        foreach (var run in paragraph.Elements<Run>().ToList()) run.Remove();

        // Drop other paragraphs in the cell that might carry stale text.
        foreach (var extra in cell.Elements<Paragraph>().Skip(1).ToList()) extra.Remove();

        // Multi-line support: when the value contains '\n', emit a <w:br/> between
        // text runs so line breaks appear correctly in Word/gotenberg. Single-line
        // values follow the original single-run path unchanged.
        if (text.Contains('\n'))
        {
            var lines = text.Split('\n');
            for (var li = 0; li < lines.Length; li++)
            {
                var lineRun = new Run();
                if (rPr is not null) lineRun.AppendChild(rPr.CloneNode(true) as RunProperties);
                lineRun.AppendChild(new Text(lines[li])
                    { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
                if (li < lines.Length - 1)
                    lineRun.AppendChild(new DocumentFormat.OpenXml.Wordprocessing.Break());
                paragraph.AppendChild(lineRun);
            }
        }
        else
        {
            var newRun = new Run();
            if (rPr is not null) newRun.AppendChild(rPr);
            newRun.AppendChild(new Text(text) { Space = DocumentFormat.OpenXml.SpaceProcessingModeValues.Preserve });
            paragraph.AppendChild(newRun);
        }
    }

    private static string ResultLabel(InspectionResultDto r) => r switch
    {
        InspectionResultDto.Pass                 => "PASS",
        InspectionResultDto.Fail                 => "FAIL",
        InspectionResultDto.FailWithObservations => "FAIL w/ OBS",
        _                                        => "—",
    };
}
