using Microsoft.Extensions.Logging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Infrastructure.Certificates;   // GotenbergClient

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>
/// Renders the Blue Sticker Annex 1 PDF. Primary: fill the official docx + Gotenberg.
/// Fallback: inline QuestPDF (same 11-column grid) when Gotenberg is unreachable.
/// </summary>
public sealed class BlueStickerReportPdfRenderer
{
    private static readonly int[] Cols =
        { 3187, 1133, 1260, 30, 1784, 110, 1305, 1431, 2540, 1440, 1525 };

    private readonly BlueStickerReportTemplateFiller _filler;
    private readonly GotenbergClient _gotenberg;
    private readonly ILogger<BlueStickerReportPdfRenderer> _log;

    static BlueStickerReportPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public BlueStickerReportPdfRenderer(BlueStickerReportTemplateFiller filler,
        GotenbergClient gotenberg, ILogger<BlueStickerReportPdfRenderer> log)
    { _filler = filler; _gotenberg = gotenberg; _log = log; }

    public async Task<byte[]> RenderAsync(BlueStickerReportDetailDto r, CancellationToken ct = default)
    {
        try
        {
            var docx = _filler.Fill(r);
            return await _gotenberg.ConvertDocxToPdfAsync(docx, $"{r.ReportNo}-Annex1.docx", ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex,
                "Blue Sticker Annex 1 docx→pdf via gotenberg failed for {Report}; using fallback.",
                r.ReportNo);
            return RenderFallback(r);
        }
    }

    private byte[] RenderFallback(BlueStickerReportDetailDto r) =>
        Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4.Landscape());
            p.Margin(20);
            p.Content().Table(t =>
            {
                t.ColumnsDefinition(cd => { foreach (var w in Cols) cd.RelativeColumn(w); });
                t.Cell().ColumnSpan(11).Background("#548DD4").Padding(8).AlignCenter()
                    .Text("LIFTING EQUIPMENT INSPECTION REPORT").FontSize(13).Bold()
                    .FontColor("#FFFFFF");
                void L(int s, string v) => t.Cell().ColumnSpan((uint)s).Background("#D9D9D9")
                    .Border(0.4f).Padding(4).AlignCenter().Text(v).FontSize(8).Bold();
                void D(int s, string? v) => t.Cell().ColumnSpan((uint)s).Border(0.4f)
                    .Padding(6).MinHeight(22).Text(string.IsNullOrWhiteSpace(v) ? " " : v)
                    .FontSize(9).Bold();
                L(1, "TUV Job Order. No."); L(3, "Aramco Category No."); L(2, "Org. Code");
                L(2, "RPO NO."); L(1, "CRM No."); L(2, "Report No.");
                D(1, r.TuvJobOrderNo); D(3, r.AramcoCategoryNo); D(2, r.OrgCode);
                D(2, r.RpoNo); D(1, r.CrmNo); D(2, r.ReportNo);
                L(4, "Department / Contractor"); L(2, "Inspection Date"); L(2, "Inspection Time");
                L(1, "Previous Sticker No."); L(2, "Previous Sticker Issued By");
                D(4, r.DepartmentContractor); D(2, r.InspectionDate?.ToString("dd MMM yyyy"));
                D(2, r.InspectionTime?.ToString("HH:mm")); D(1, r.PreviousStickerNo);
                D(2, r.PreviousStickerIssuedBy);
                L(1, "Area of Inspection"); L(3, "Equipment ID No."); L(2, "Capacity");
                L(2, "Equipment Location"); L(1, "Inspection Result"); L(2, "New Sticker No.");
                D(1, r.AreaOfInspection); D(3, r.EquipmentIdNo); D(2, r.Capacity);
                D(2, r.EquipmentLocation);
                D(1, r.Result switch {
                    BlueStickerResultDto.Pass => "PASS",
                    BlueStickerResultDto.Fail => "FAIL",
                    _ => "—"
                });
                D(2, r.NewStickerNo);
                L(1, "Manufacturer"); L(3, "Model"); L(4, "Equipment Type");
                L(1, "Equipment Serial No."); L(2, "Sticker Expiration Date");
                D(1, r.Manufacturer); D(3, r.Model); D(4, r.EquipmentType);
                D(1, r.EquipmentSerialNo); D(2, r.StickerExpirationDate?.ToString("dd MMM yyyy"));
                t.Cell().ColumnSpan(8).Background("#548DD4").Padding(4).AlignCenter()
                    .Text("DEFICIENCES / OBSERVATIONS").FontSize(9).Bold().FontColor("#FFFFFF");
                t.Cell().ColumnSpan(3).Background("#548DD4").Padding(4).AlignCenter()
                    .Text("CORRECTIVE ACTION TAKEN").FontSize(9).Bold().FontColor("#FFFFFF");
                t.Cell().ColumnSpan(8).Border(0.4f).Padding(8).MinHeight(60)
                    .Text(r.Deficiencies ?? " ").FontSize(8.5f);
                t.Cell().ColumnSpan(3).Border(0.4f).Padding(8).MinHeight(60)
                    .Text(r.CorrectiveActionsTaken ?? " ").FontSize(8.5f);

                // ── Signature section (mirrors AramcoReportPdfRenderer fallback) ──
                void BH(int s, string v) =>
                    t.Cell().ColumnSpan((uint)s).Background("#548DD4").Border(0.4f)
                        .Padding(4).AlignCenter().Text(v).FontSize(9).Bold().FontColor("#FFFFFF");

                // Blue header row: 3 + 5 + 3 = 11
                BH(3, "RECIEVER");
                BH(5, "INSPECTOR");
                BH(3, "TECHNICAL REVIEWER");

                // Label row: 1+1+1 | 2+2+1 | 1+1+1 = 11
                L(1, "Name"); L(1, "Badge No."); L(1, "Telephone #");
                L(2, "Name"); L(2, "SAP NO."); L(1, "Telephone #");
                L(1, "Name"); L(1, "Received date"); L(1, "Reviewed date");

                // Data row: 1+1+1 | 2+2+1 | 1+1+1 = 11
                D(1, r.ReceiverName); D(1, r.ReceiverBadgeNo); D(1, r.ReceiverTelephone);
                D(2, r.InspectorName); D(2, r.InspectorSapNo); D(1, r.InspectorTelephone);
                D(1, r.TechnicalReviewerName);
                D(1, r.ReceivedDate?.ToString("dd MMM yyyy"));
                D(1, r.ReviewedDate?.ToString("dd MMM yyyy"));

                // Signature row: 3 + 5 + 3 = 11. Embed the captured PNGs when present;
                // otherwise show a faint "Signature" placeholder so the cell isn't blank.
                SignatureCell(t, 3, r.ReceiverSignaturePng);
                SignatureCell(t, 5, r.InspectorSignaturePng);
                SignatureCell(t, 3, r.TechnicalReviewerSignaturePng);
            });
        })).GeneratePdf();

    private static void SignatureCell(TableDescriptor t, uint span, string? dataUrl)
    {
        var png = DecodePng(dataUrl);
        var cell = t.Cell().ColumnSpan(span).Border(0.4f).Padding(6).MinHeight(40);
        if (png is null)
        {
            cell.Text("Signature").FontSize(7).FontColor("#64748b");
            return;
        }
        cell.AlignCenter().AlignMiddle().Height(40).Image(png).FitArea();
    }

    private static byte[]? DecodePng(string? dataUrl)
    {
        if (string.IsNullOrWhiteSpace(dataUrl)) return null;
        var comma = dataUrl.IndexOf(',');
        if (comma < 0) return null;
        try { return Convert.FromBase64String(dataUrl[(comma + 1)..]); }
        catch (FormatException) { return null; }
    }
}
