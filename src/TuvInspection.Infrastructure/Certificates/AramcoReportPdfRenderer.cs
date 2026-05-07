using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Infrastructure.Certificates;

/// <summary>
/// Renders the Aramco-approved Annex 1 (MS0053813) Lifting Equipment Inspection Report
/// in a layout that matches the official Word template byte-for-byte:
///   - 11-column grid sized in the same dxa proportions as the docx
///     (3187/1133/1260/30/1784/110/1305/1431/2540/1440/1525)
///   - Blue title bar (#548DD4) and grey label rows (#D9D9D9) per the original
///   - Same row order: title → 4 detail rows → compliance paragraph → deficiencies →
///     signatures
/// Aramco fields come from <c>InspectionCertificate.AramcoReportJson</c>; equipment
/// + inspector context is supplied by the caller (handler) since they don't live on the
/// cert DTO.
/// </summary>
public sealed class AramcoReportPdfRenderer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // Palette taken directly from the docx theme.
    private const string TitleBlue  = "#548DD4";
    private const string LabelGrey  = "#D9D9D9";
    private const string PassGreen  = "#00B050";
    private const string FailRed    = "#C00000";
    private const string White      = "#FFFFFF";
    private const string Ink        = "#000000";

    // 11-column grid weights from the original docx tblGrid.
    private static readonly int[] Cols =
        { 3187, 1133, 1260, 30, 1784, 110, 1305, 1431, 2540, 1440, 1525 };

    static AramcoReportPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] Render(CertificateDetailDto cert,
        string inspectorName, string? inspectorSapNo, string equipmentSwl)
    {
        var data = ParseAramcoData(cert.AramcoReportJson);

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
        null, null, null, null, null, null, null, null, null, null, null, null, null);

    private static void Sheet(IContainer c, CertificateDetailDto cert, AramcoReportData data,
        string inspectorName, string? inspectorSapNo, string equipmentSwl)
    {
        c.Table(t =>
        {
            t.ColumnsDefinition(cd =>
            {
                foreach (var w in Cols) cd.RelativeColumn(w);
            });

            // ── Row 0: title bar — span all 11 columns ─────────────────────────
            t.Cell().ColumnSpan(11)
                .Background(TitleBlue).Padding(8).AlignCenter()
                .Text("LIFTING EQUIPMENT INSPECTION REPORT")
                .FontSize(13).Bold().FontColor(White);

            // ── Row 1: 6 labels — spans [1,3,2,2,1,2] ─────────────────────────
            LabelCell(t, 1, "TUV Job Order. No.");
            LabelCell(t, 3, "Aramco Category No.");
            LabelCell(t, 2, "Org. Code");
            LabelCell(t, 2, "RPO NO.");
            LabelCell(t, 1, "CRM No.");
            LabelCell(t, 2, "Report No.");

            // ── Row 2: data ───────────────────────────────────────────────────
            DataCell(t, 1, data.TuvJobOrderNo);
            DataCell(t, 3, data.AramcoCategoryNo ?? cert.EquipmentAramcoCategory);
            DataCell(t, 2, data.OrgCode);
            DataCell(t, 2, data.RpoNo);
            DataCell(t, 1, data.CrmNo);
            DataCell(t, 2, data.ReportNo ?? cert.CertificateNo);

            // ── Row 3: 5 labels — spans [4,2,2,1,2] ───────────────────────────
            LabelCell(t, 4, "Department / Contractor");
            LabelCell(t, 2, "Inspection Date");
            LabelCell(t, 2, "Inspection Time");
            LabelCell(t, 1, "Previous Sticker No.");
            LabelCell(t, 2, "Previous Sticker Issued By");

            // ── Row 4: data ───────────────────────────────────────────────────
            DataCell(t, 4, data.DepartmentContractor);
            DataCell(t, 2, cert.InspectionDate.ToString("dd MMM yyyy"));
            DataCell(t, 2, data.InspectionTime?.ToString("HH:mm"));
            DataCell(t, 1, data.PreviousStickerNo);
            DataCell(t, 2, data.PreviousStickerIssuedBy);

            // ── Row 5: 6 labels — spans [1,3,2,2,1,2] ─────────────────────────
            LabelCell(t, 1, "Area of Inspection");
            LabelCell(t, 3, "Equipment ID No.");
            LabelCell(t, 2, "Capacity");
            LabelCell(t, 2, "Equipment Location");
            LabelCell(t, 1, "Inspection Result");
            LabelCell(t, 2, "New Sticker No.");

            // ── Row 6: data — Inspection Result cell coloured by outcome ──────
            DataCell(t, 1, data.AreaOfInspection);
            DataCell(t, 3, cert.EquipmentIdNo);
            DataCell(t, 2, data.Capacity ?? equipmentSwl);
            DataCell(t, 2, data.EquipmentLocationOnSite);
            ResultCell(t, cert.Result);
            DataCell(t, 2, cert.StickerNo, monospace: true);

            // ── Row 7: 5 labels — spans [1,3,4,1,2] ───────────────────────────
            LabelCell(t, 1, "Manufacturer");
            LabelCell(t, 3, "Model");
            LabelCell(t, 4, "Equipment Type");
            LabelCell(t, 1, "Equipment Serial No.");
            LabelCell(t, 2, "Sticker Expiration Date");

            // ── Row 8: data ───────────────────────────────────────────────────
            DataCell(t, 1, data.Manufacturer);
            DataCell(t, 3, data.Model);
            DataCell(t, 4, cert.EquipmentTypeName);
            DataCell(t, 1, data.EquipmentSerialNo);
            DataCell(t, 2, (data.StickerExpirationDate ?? cert.NextDueDate)?.ToString("dd MMM yyyy"));

            // ── Row 9: compliance paragraph — full width grey ─────────────────
            t.Cell().ColumnSpan(11)
                .Background(LabelGrey).Border(0.4f).BorderColor(Ink).Padding(8)
                .Text(
                    "The above-mentioned equipment was inspected by TUV Rheinland Arabia LLC " +
                    "in accordance with applicable Saudi Aramco G.I.'s, ASME AND API Standards. " +
                    "Deficiencies that required corrective action are listed below. Specific repair " +
                    "to correct each deficiency should be noted in the corrective action taken column.")
                .FontSize(8).Italic();

            // ── Row 10: deficiencies / corrective actions header ──────────────
            BlueHeaderCell(t, 8, "DEFICIENCES / OBSERVATIONS");
            BlueHeaderCell(t, 3, "CORRECTIVE ACTION TAKEN");

            // ── Row 11: deficiencies / corrective actions data ────────────────
            t.Cell().ColumnSpan(8).Border(0.4f).BorderColor(Ink).Padding(8).MinHeight(70)
                .Text(string.IsNullOrWhiteSpace(data.Deficiencies) ? " " : data.Deficiencies)
                .FontSize(8.5f);
            t.Cell().ColumnSpan(3).Border(0.4f).BorderColor(Ink).Padding(8).MinHeight(70)
                .Text(string.IsNullOrWhiteSpace(data.CorrectiveActionsTaken) ? " " : data.CorrectiveActionsTaken)
                .FontSize(8.5f);

            // ── Row 12: signature header — RECIEVER | INSPECTOR | TECH REV ────
            BlueHeaderCell(t, 3, "RECIEVER");
            BlueHeaderCell(t, 5, "INSPECTOR");
            BlueHeaderCell(t, 3, "TECHNICAL REVIEWER");

            // ── Row 13: signature labels ──────────────────────────────────────
            // Receiver block (3 cols)
            LabelCell(t, 1, "Name");
            LabelCell(t, 1, "Badge No.");
            LabelCell(t, 1, "Telephone #");
            // Inspector block (5 cols → split [2,2,1])
            LabelCell(t, 2, "Name");
            LabelCell(t, 2, "SAP NO.");
            LabelCell(t, 1, "Telephone #");
            // Tech reviewer block (3 cols)
            LabelCell(t, 1, "Name");
            LabelCell(t, 1, "Received date");
            LabelCell(t, 1, "Reviewed date");

            // ── Row 14: signature data ────────────────────────────────────────
            DataCell(t, 1, data.ReceiverName);
            DataCell(t, 1, data.ReceiverBadgeNo);
            DataCell(t, 1, data.ReceiverTelephone);
            DataCell(t, 2, inspectorName);
            DataCell(t, 2, inspectorSapNo);
            DataCell(t, 1, data.InspectorTelephone);
            DataCell(t, 1, null);
            DataCell(t, 1, data.ReceivedDate?.ToString("dd MMM yyyy"));
            DataCell(t, 1, data.ReviewedDate?.ToString("dd MMM yyyy"));

            // ── Row 15: signature lines ───────────────────────────────────────
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

    private static void DataCell(TableDescriptor t, int span, string? value, bool monospace = false)
    {
        var item = t.Cell().ColumnSpan((uint)span)
            .Background(White).Border(0.4f).BorderColor(Ink).Padding(6).MinHeight(22);
        item.Text(string.IsNullOrWhiteSpace(value) ? " " : value)
            .FontSize(9).Bold();
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
