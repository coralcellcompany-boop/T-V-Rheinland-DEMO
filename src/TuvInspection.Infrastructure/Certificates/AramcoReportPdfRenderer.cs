using System.Text.Json;
using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Infrastructure.Certificates;

/// <summary>
/// Renders the Aramco Annex 1 (MS0053813) inspection report.
///
/// Primary path — the embedded official .docx template is filled in memory by
/// <see cref="Annex1TemplateFiller"/> and converted to PDF by
/// <see cref="GotenbergClient"/> (LibreOffice headless). This produces a PDF
/// byte-for-byte identical to the Aramco-approved Word layout.
///
/// Fallback path — when gotenberg is unreachable (dev machines without the
/// container running, CI, etc.), an inline QuestPDF renderer reproduces the same
/// 11-column grid so the workflow doesn't break. The fallback is logged loudly so
/// operators notice the missing dependency.
/// </summary>
public sealed class AramcoReportPdfRenderer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string TitleBlue = "#548DD4";
    private const string LabelGrey = "#D9D9D9";
    private const string PassGreen = "#00B050";
    private const string FailRed   = "#C00000";
    private const string White     = "#FFFFFF";
    private const string Ink       = "#000000";

    private static readonly int[] Cols =
        { 3187, 1133, 1260, 30, 1784, 110, 1305, 1431, 2540, 1440, 1525 };

    private readonly Annex1TemplateFiller _filler;
    private readonly GotenbergClient _gotenberg;
    private readonly ILogger<AramcoReportPdfRenderer> _log;

    static AramcoReportPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public AramcoReportPdfRenderer(Annex1TemplateFiller filler,
        GotenbergClient gotenberg, ILogger<AramcoReportPdfRenderer> log)
    {
        _filler = filler;
        _gotenberg = gotenberg;
        _log = log;
    }

    public async Task<byte[]> RenderAsync(CertificateDetailDto cert,
        string inspectorName, string? inspectorSapNo, string equipmentSwl,
        CancellationToken ct = default)
    {
        var data = ParseAramcoData(cert.AramcoReportJson);

        // Primary: fill the official docx and let gotenberg convert it.
        try
        {
            var docx = _filler.Fill(cert, data, inspectorName, inspectorSapNo, equipmentSwl);
            var pdf = await _gotenberg.ConvertDocxToPdfAsync(
                docx, $"{cert.CertificateNo}-Annex1.docx", ct);
            return pdf;
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Annex 1 docx→pdf via gotenberg failed for cert {Cert}; falling back to inline renderer.",
                cert.CertificateNo);
            return RenderFallback(cert, data, inspectorName, inspectorSapNo, equipmentSwl);
        }
    }

    private static AramcoReportData ParseAramcoData(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Empty;
        try
        {
            return JsonSerializer.Deserialize<AramcoReportData>(json, JsonOpts) ?? Empty;
        }
        catch
        {
            return Empty;
        }
    }

    private static readonly AramcoReportData Empty = new(
        null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null, null, null, null, null, null, null, null, null,
        null, null, null, null);

    // ─── Fallback (inline QuestPDF) ─────────────────────────────────────────────
    // Same 11-column grid as the docx so the layout is reasonably faithful even
    // when gotenberg isn't running. Runs in-process, no external deps.

    private byte[] RenderFallback(CertificateDetailDto cert, AramcoReportData data,
        string inspectorName, string? inspectorSapNo, string equipmentSwl)
    {
        return Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4.Landscape());
                page.Margin(20);
                page.PageColor(White);
                page.DefaultTextStyle(t => t.FontSize(8.5f).FontColor(Ink).FontFamily("Helvetica"));
                page.Content().Element(el => Sheet(el, cert, data, inspectorName, inspectorSapNo, equipmentSwl));
            });
        }).GeneratePdf();
    }

    private static void Sheet(IContainer c, CertificateDetailDto cert, AramcoReportData data,
        string inspectorName, string? inspectorSapNo, string equipmentSwl)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                foreach (var w in Cols) cd.RelativeColumn(w);
            });

            t.Cell().ColumnSpan(11)
                .Background(TitleBlue).Padding(8).AlignCenter()
                .Text("LIFTING EQUIPMENT INSPECTION REPORT")
                .FontSize(13).Bold().FontColor(White);

            LabelCell(t, 1, "TUV Job Order. No.");
            LabelCell(t, 3, "Aramco Category No.");
            LabelCell(t, 2, "Org. Code");
            LabelCell(t, 2, "RPO NO.");
            LabelCell(t, 1, "CRM No.");
            LabelCell(t, 2, "Report No.");

            DataCell(t, 1, data.TuvJobOrderNo);
            DataCell(t, 3, data.AramcoCategoryNo ?? cert.EquipmentAramcoCategory);
            DataCell(t, 2, data.OrgCode);
            DataCell(t, 2, data.RpoNo);
            DataCell(t, 1, data.CrmNo);
            DataCell(t, 2, data.ReportNo ?? cert.CertificateNo);

            LabelCell(t, 4, "Department / Contractor");
            LabelCell(t, 2, "Inspection Date");
            LabelCell(t, 2, "Inspection Time");
            LabelCell(t, 1, "Previous Sticker No.");
            LabelCell(t, 2, "Previous Sticker Issued By");

            DataCell(t, 4, data.DepartmentContractor);
            DataCell(t, 2, cert.InspectionDate.ToString("dd MMM yyyy"));
            DataCell(t, 2, data.InspectionTime?.ToString("HH:mm"));
            DataCell(t, 1, data.PreviousStickerNo);
            DataCell(t, 2, data.PreviousStickerIssuedBy);

            LabelCell(t, 1, "Area of Inspection");
            LabelCell(t, 3, "Equipment ID No.");
            LabelCell(t, 2, "Capacity");
            LabelCell(t, 2, "Equipment Location");
            LabelCell(t, 1, "Inspection Result");
            LabelCell(t, 2, "New Sticker No.");

            DataCell(t, 1, data.AreaOfInspection);
            DataCell(t, 3, cert.EquipmentIdNo);
            DataCell(t, 2, data.Capacity ?? equipmentSwl);
            DataCell(t, 2, data.EquipmentLocationOnSite);
            ResultCell(t, cert.Result);
            DataCell(t, 2, cert.StickerNo);

            LabelCell(t, 1, "Manufacturer");
            LabelCell(t, 3, "Model");
            LabelCell(t, 4, "Equipment Type");
            LabelCell(t, 1, "Equipment Serial No.");
            LabelCell(t, 2, "Sticker Expiration Date");

            DataCell(t, 1, data.Manufacturer);
            DataCell(t, 3, data.Model);
            DataCell(t, 4, cert.EquipmentTypeName);
            DataCell(t, 1, data.EquipmentSerialNo);
            DataCell(t, 2, (data.StickerExpirationDate ?? cert.NextDueDate)?.ToString("dd MMM yyyy"));

            t.Cell().ColumnSpan(11)
                .Background(LabelGrey).Border(0.4f).BorderColor(Ink).Padding(8)
                .Text(
                    "The above-mentioned equipment was inspected by TUV Rheinland Arabia LLC " +
                    "in accordance with applicable Saudi Aramco G.I.'s, ASME AND API Standards. " +
                    "Deficiencies that required corrective action are listed below. Specific repair " +
                    "to correct each deficiency should be noted in the corrective action taken column.")
                .FontSize(8).Italic();

            BlueHeaderCell(t, 8, "DEFICIENCES / OBSERVATIONS");
            BlueHeaderCell(t, 3, "CORRECTIVE ACTION TAKEN");

            // Build deficiency / corrective-action text from structured items when present;
            // fall back to the legacy free-text scalars for older certificates.
            string deficienciesText;
            string correctiveText;
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
                deficienciesText = string.IsNullOrWhiteSpace(data.Deficiencies) ? " " : data.Deficiencies;
                correctiveText = string.IsNullOrWhiteSpace(data.CorrectiveActionsTaken) ? " " : data.CorrectiveActionsTaken;
            }

            t.Cell().ColumnSpan(8).Border(0.4f).BorderColor(Ink).Padding(8).MinHeight(70)
                .Text(string.IsNullOrWhiteSpace(deficienciesText) ? " " : deficienciesText)
                .FontSize(8.5f);
            t.Cell().ColumnSpan(3).Border(0.4f).BorderColor(Ink).Padding(8).MinHeight(70)
                .Text(string.IsNullOrWhiteSpace(correctiveText) ? " " : correctiveText)
                .FontSize(8.5f);

            BlueHeaderCell(t, 3, "RECIEVER");
            BlueHeaderCell(t, 5, "INSPECTOR");
            BlueHeaderCell(t, 3, "TECHNICAL REVIEWER");

            LabelCell(t, 1, "Name");
            LabelCell(t, 1, "Badge No.");
            LabelCell(t, 1, "Telephone #");
            LabelCell(t, 2, "Name");
            LabelCell(t, 2, "SAP NO.");
            LabelCell(t, 1, "Telephone #");
            LabelCell(t, 1, "Name");
            LabelCell(t, 1, "Received date");
            LabelCell(t, 1, "Reviewed date");

            DataCell(t, 1, data.ReceiverName);
            DataCell(t, 1, data.ReceiverBadgeNo);
            DataCell(t, 1, data.ReceiverTelephone);
            DataCell(t, 2, inspectorName);
            DataCell(t, 2, inspectorSapNo);
            DataCell(t, 1, data.InspectorTelephone);
            DataCell(t, 1, null);
            DataCell(t, 1, data.ReceivedDate?.ToString("dd MMM yyyy"));
            DataCell(t, 1, data.ReviewedDate?.ToString("dd MMM yyyy"));

            SignatureCell(t, 3);
            SignatureCell(t, 5);
            SignatureCell(t, 3);
        });
    }

    private static void LabelCell(TableDescriptor t, int span, string label)
    {
        t.Cell().ColumnSpan((uint)span)
            .Background(LabelGrey).Border(0.4f).BorderColor(Ink).Padding(4).AlignCenter()
            .Text(label).FontSize(8).Bold();
    }

    private static void DataCell(TableDescriptor t, int span, string? value)
    {
        t.Cell().ColumnSpan((uint)span)
            .Background(White).Border(0.4f).BorderColor(Ink).Padding(6).MinHeight(22)
            .Text(string.IsNullOrWhiteSpace(value) ? " " : value).FontSize(9).Bold();
    }

    private static void BlueHeaderCell(TableDescriptor t, int span, string label)
    {
        t.Cell().ColumnSpan((uint)span)
            .Background(TitleBlue).Border(0.4f).BorderColor(Ink).Padding(4).AlignCenter()
            .Text(label).FontSize(9).Bold().FontColor(White);
    }

    private static void ResultCell(TableDescriptor t, InspectionResultDto result)
    {
        var (label, color) = result switch
        {
            InspectionResultDto.Pass                 => ("PASS", PassGreen),
            InspectionResultDto.Fail                 => ("FAIL", FailRed),
            InspectionResultDto.FailWithObservations => ("FAIL w/ OBS", FailRed),
            _                                        => ("—", White),
        };
        t.Cell().ColumnSpan(1)
            .Background(color).Border(0.4f).BorderColor(Ink).Padding(4).AlignCenter()
            .Text(label).FontSize(10).Bold()
            .FontColor(color == White ? Ink : White);
    }

    private static void SignatureCell(TableDescriptor t, int span)
    {
        t.Cell().ColumnSpan((uint)span)
            .Background(White).Border(0.4f).BorderColor(Ink).Padding(6).MinHeight(40)
            .Text("Signature").FontSize(7).FontColor("#64748b");
    }
}
