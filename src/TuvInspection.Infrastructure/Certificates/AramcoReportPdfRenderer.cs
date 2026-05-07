using System.Text.Json;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Infrastructure.Certificates;

/// <summary>
/// Renders the Aramco-approved Annex 1 (MS0053813) Lifting Equipment Inspection Report
/// matching the official format used by TÜV Rheinland Arabia for Saudi Aramco.
///
/// The report payload (RPO No., CRM No., Org Code, Aramco Category No., previous
/// sticker info, receiver/inspector signatures, etc.) is stored as JSON in
/// <c>InspectionCertificate.AramcoReportJson</c> and unfolded here. Fields that
/// are not yet filled render as a blank line so the printed PDF can be hand-completed
/// in the field.
/// </summary>
public sealed class AramcoReportPdfRenderer
{
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    private const string HeaderBlue = "#548dd4";
    private const string SoftGrey   = "#d9d9d9";
    private const string White      = "#ffffff";
    private const string Ink        = "#0f172a";

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
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(Ink));
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
        c.Column(col =>
        {
            // Title bar
            col.Item().Background(HeaderBlue).Padding(8).AlignCenter()
                .Text("LIFTING EQUIPMENT INSPECTION REPORT")
                .FontSize(13).Bold().FontColor(White);

            // Row 1 — TUV Job Order, Aramco Cat No., Org Code, RPO No., CRM No., Report No.
            HeaderRow6(col,
                ("TUV Job Order No.", data.TuvJobOrderNo),
                ("Aramco Category No.", data.AramcoCategoryNo),
                ("Org. Code", data.OrgCode),
                ("RPO No.", data.RpoNo),
                ("CRM No.", data.CrmNo),
                ("Report No.", data.ReportNo ?? cert.CertificateNo));

            // Row 2 — Department/Contractor, Inspection Date/Time, Previous Sticker info
            HeaderRow5(col,
                ("Department / Contractor", data.DepartmentContractor),
                ("Inspection Date", cert.InspectionDate.ToString("dd MMM yyyy")),
                ("Inspection Time", data.InspectionTime?.ToString("HH:mm")),
                ("Previous Sticker No.", data.PreviousStickerNo),
                ("Previous Sticker Issued By", data.PreviousStickerIssuedBy));

            // Row 3 — Area, Equipment ID, Capacity, Location, Result, New Sticker
            HeaderRow6(col,
                ("Area of Inspection", data.AreaOfInspection),
                ("Equipment ID No.", cert.EquipmentIdNo),
                ("Capacity", data.Capacity ?? equipmentSwl),
                ("Equipment Location", data.EquipmentLocationOnSite),
                ("Inspection Result", cert.Result.ToString()),
                ("New Sticker No.", cert.StickerNo));

            // Row 4 — Manufacturer, Model, Equipment Type, Serial No., Sticker Expiration
            HeaderRow5(col,
                ("Manufacturer", data.Manufacturer),
                ("Model", data.Model),
                ("Equipment Type", cert.EquipmentTypeName),
                ("Equipment Serial No.", data.EquipmentSerialNo),
                ("Sticker Expiration Date",
                    (data.StickerExpirationDate ?? cert.NextDueDate)?.ToString("dd MMM yyyy")));

            // Compliance statement
            col.Item().PaddingTop(8).Border(0.5f).Padding(8).Text(
                "The above-mentioned equipment was inspected by TUV Rheinland Arabia LLC in " +
                "accordance with applicable Saudi Aramco G.I.'s, ASME and API Standards. " +
                "Deficiencies that require corrective action are listed below; specific repair " +
                "to correct each deficiency should be noted in the corrective action taken column."
            ).FontSize(8).Italic();

            // Deficiencies / Corrective Actions table
            col.Item().PaddingTop(8).Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); });
                t.Header(h =>
                {
                    h.Cell().Background(SoftGrey).Padding(4).Text("DEFICIENCIES / OBSERVATIONS").Bold();
                    h.Cell().Background(SoftGrey).Padding(4).Text("CORRECTIVE ACTION TAKEN").Bold();
                });
                t.Cell().Border(0.5f).Padding(6).MinHeight(80)
                    .Text(string.IsNullOrWhiteSpace(data.Deficiencies) ? "—" : data.Deficiencies).FontSize(8);
                t.Cell().Border(0.5f).Padding(6).MinHeight(80)
                    .Text(string.IsNullOrWhiteSpace(data.CorrectiveActionsTaken) ? "—" : data.CorrectiveActionsTaken).FontSize(8);
            });

            // Signature block — Receiver | Inspector | Tech Reviewer
            col.Item().PaddingTop(10).Table(t =>
            {
                t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                t.Header(h =>
                {
                    h.Cell().Background(HeaderBlue).Padding(4).AlignCenter()
                        .Text("RECEIVER").Bold().FontColor(White);
                    h.Cell().Background(HeaderBlue).Padding(4).AlignCenter()
                        .Text("INSPECTOR").Bold().FontColor(White);
                    h.Cell().Background(HeaderBlue).Padding(4).AlignCenter()
                        .Text("TECHNICAL REVIEWER").Bold().FontColor(White);
                });

                SignatureCell(t, "Name",       data.ReceiverName);
                SignatureCell(t, "Name",       inspectorName);
                SignatureCell(t, "Name",       null);

                SignatureCell(t, "Badge No.",  data.ReceiverBadgeNo);
                SignatureCell(t, "SAP No.",    inspectorSapNo);
                SignatureCell(t, "Reviewed",   data.ReviewedDate?.ToString("dd MMM yyyy"));

                SignatureCell(t, "Telephone",  data.ReceiverTelephone);
                SignatureCell(t, "Telephone",  data.InspectorTelephone);
                SignatureCell(t, "Received",   data.ReceivedDate?.ToString("dd MMM yyyy"));

                SignatureCell(t, "Signature",  null, isSignature: true);
                SignatureCell(t, "Signature",  null, isSignature: true);
                SignatureCell(t, "Signature",  null, isSignature: true);
            });

            col.Item().PaddingTop(10).Text(
                "Annex 1 — MS0053813 — TÜV Rheinland Arabia LLC — Aramco-approved contractor agency.")
                .FontSize(6).FontColor("#64748b").AlignCenter();
        });
    }

    private static void HeaderRow6(ColumnDescriptor col, params (string Label, string? Value)[] cells)
    {
        col.Item().PaddingTop(2).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (var i = 0; i < cells.Length; i++) c.RelativeColumn();
            });
            t.Header(h =>
            {
                foreach (var (label, _) in cells)
                    h.Cell().Background(SoftGrey).Padding(4).AlignCenter().Text(label).FontSize(7).Bold();
            });
            foreach (var (_, value) in cells)
                t.Cell().Border(0.5f).Padding(6).MinHeight(20)
                    .Text(string.IsNullOrWhiteSpace(value) ? " " : value).FontSize(8);
        });
    }

    private static void HeaderRow5(ColumnDescriptor col, params (string Label, string? Value)[] cells)
    {
        col.Item().PaddingTop(2).Table(t =>
        {
            t.ColumnsDefinition(c =>
            {
                for (var i = 0; i < cells.Length; i++) c.RelativeColumn();
            });
            t.Header(h =>
            {
                foreach (var (label, _) in cells)
                    h.Cell().Background(SoftGrey).Padding(4).AlignCenter().Text(label).FontSize(7).Bold();
            });
            foreach (var (_, value) in cells)
                t.Cell().Border(0.5f).Padding(6).MinHeight(20)
                    .Text(string.IsNullOrWhiteSpace(value) ? " " : value).FontSize(8);
        });
    }

    private static void SignatureCell(TableDescriptor t, string label, string? value,
        bool isSignature = false)
    {
        t.Cell().Border(0.5f).Padding(6).Column(col =>
        {
            col.Item().Text(label).FontSize(6).FontColor("#64748b");
            if (isSignature)
                col.Item().Height(36);
            else
                col.Item().Text(string.IsNullOrWhiteSpace(value) ? " " : value).FontSize(9).Bold();
        });
    }
}
