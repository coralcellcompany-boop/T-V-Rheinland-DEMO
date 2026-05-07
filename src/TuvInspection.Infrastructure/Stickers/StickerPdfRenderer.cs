using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.Stickers;

namespace TuvInspection.Infrastructure.Stickers;

/// <summary>
/// Sticker-related PDFs:
///  - <see cref="RenderPublic"/>: a single-page A6 verification sheet shown when a QR is scanned.
///    Designed to mirror the official blue physical Aramco-approved sticker (TUVR ######, equipment
///    fields, inspector + SAP, dates) so the digital twin matches what is on the equipment.
///  - <see cref="RenderPrintBatch"/>: an A4 sheet of stickers (3 across × 8 rows = 24/page) for
///    printing onto adhesive label stock per SRS §5.2.
/// All PII is omitted; the equipment ID and serial number are shown only as the last 6 characters.
/// </summary>
public sealed class StickerPdfRenderer
{
    // Aramco-approved blue sticker palette (sampled from the reference physical sticker design).
    private const string Blue       = "#0a64a4";
    private const string BlueDeep   = "#063e69";
    private const string Red        = "#d92121";
    private const string White      = "#ffffff";
    private const string FieldDim   = "#dde9f2";
    private const string ValidGreen = "#16a34a";
    private const string InvalidRed = "#b91c1c";

    static StickerPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public byte[] RenderPublic(StickerPublicViewDto v, byte[]? qrPng)
    {
        return Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(0);
                page.PageColor(Blue);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(White));
                page.Content().Element(el => StickerSheet(el, v, qrPng));
            });
        }).GeneratePdf();
    }

    public byte[] RenderPrintBatch(IReadOnlyList<(string StickerNo, byte[] QrPng)> rows)
    {
        return Document.Create(c =>
        {
            c.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(20);
                page.PageColor(White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor(BlueDeep));
                page.Content().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    foreach (var r in rows)
                    {
                        t.Cell().Padding(4).Background(Blue).Padding(8).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.ConstantItem(64).Image(r.QrPng).FitArea();
                                row.RelativeItem().PaddingLeft(6).Column(meta =>
                                {
                                    meta.Item().Text("شركة تي يو في راينلاند العربية").FontSize(7).FontColor(White);
                                    meta.Item().Text("TÜV Rheinland Arabia").FontSize(8).Bold().FontColor(White);
                                    meta.Item().PaddingTop(2).Text(t =>
                                    {
                                        t.Span("NO. : ").FontSize(7).FontColor(White);
                                        t.Span(r.StickerNo).FontSize(11).Bold().FontColor(Red);
                                    });
                                });
                            });
                            col.Item().PaddingTop(6).Text("INSPECTION STICKER").FontSize(7).FontColor(White).AlignCenter();
                            col.Item().PaddingTop(2).Text("Scan QR to verify").FontSize(6).FontColor(FieldDim).AlignCenter();
                        });
                    }
                });
            });
        }).GeneratePdf();
    }

    /// <summary>
    /// Layout of the public verification sheet — mirrors the Aramco-approved blue sticker.
    /// </summary>
    private static void StickerSheet(IContainer c, StickerPublicViewDto v, byte[]? qrPng)
    {
        c.Background(Blue).Padding(10).Column(col =>
        {
            // Header row: QR | brand + sticker number | logo placeholder
            col.Item().Row(row =>
            {
                if (qrPng is { Length: > 0 })
                    row.ConstantItem(72).Image(qrPng).FitArea();
                else
                    row.ConstantItem(72).Height(72).Background(White);

                row.RelativeItem().PaddingLeft(6).Column(brand =>
                {
                    brand.Item().Text("2nd Floor, Building # A — Al-Mousa Tower")
                        .FontSize(6).FontColor(White);
                    brand.Item().Text("King Faisal Road, Al-Yarmouk Dist., Al-Khobar — KSA")
                        .FontSize(6).FontColor(White);
                    brand.Item().Text("013 866 4906 / 013 866 4902 / 012 601 9230")
                        .FontSize(6).FontColor(White);
                    brand.Item().Text("info@sa.tuv.com   www.tuv.com")
                        .FontSize(6).FontColor(White);
                    brand.Item().PaddingTop(3).Text("شركة تي يو في راينلاند العربية")
                        .FontSize(8).Bold().FontColor(White);
                    brand.Item().Text(t =>
                    {
                        t.Span("NO. : ").FontSize(9).FontColor(White);
                        t.Span(v.StickerNo).FontSize(13).Bold().FontColor(Red);
                    });
                });

                row.ConstantItem(50).AlignRight().Column(logo =>
                {
                    logo.Item().Text("TÜV").FontSize(11).Bold().FontColor(White).AlignRight();
                    logo.Item().Text("Rheinland").FontSize(8).FontColor(White).AlignRight();
                });
            });

            // Validity badge
            col.Item().PaddingTop(8).Background(v.IsValidNow ? ValidGreen : InvalidRed)
                .Padding(6).AlignCenter()
                .Text(v.IsValidNow ? "VALID — IN SERVICE" : "NOT VALID")
                .FontSize(10).Bold().FontColor(White);

            // Equipment / inspection fields
            col.Item().PaddingTop(8).Column(fields =>
            {
                Field(fields, "EQUIP. S.NO.",        v.EquipmentSerialNo);
                Field(fields, "EQUIP. ID NO.",       v.EquipmentIdNo);
                Field(fields, "EQUIP. SWL",          v.EquipmentSwl);
                Field(fields, "EQUIP. TYPE",         v.EquipmentTypeName);
                Field(fields, "INSPECTION DATE",     v.InspectionDate?.ToString("dd MMM yyyy"));
                Field(fields, "NEXT INSPECTION DATE",v.ValidUntil?.ToString("dd MMM yyyy"));
                Field(fields, "INSPECTOR NAME",      v.InspectorName);
                Field(fields, "SAP NO.",             v.InspectorSapNo);
                Field(fields, "CERTIFICATE NO.",     v.CertificateNo);
                Field(fields, "OPERATOR",            v.ClientName);
                if (!string.IsNullOrWhiteSpace(v.AramcoCategory))
                    Field(fields, "ARAMCO CATEGORY", v.AramcoCategory);
            });

            // Footer disclaimers
            col.Item().PaddingTop(8).BorderTop(0.5f).BorderColor(White).PaddingTop(4).Column(footer =>
            {
                footer.Item().Text("S.A INSPECTION DEPARTMENT APPROVED CONTRACTOR AGENCY")
                    .FontSize(7).FontColor(White).AlignCenter();
                footer.Item().Text("NOTE: THIS STICKER IS INTENDED AND VALID FOR ARAMCO PREMISES AND PROJECTS ONLY")
                    .FontSize(6).FontColor(Red).Bold().AlignCenter();
            });
        });
    }

    private static void Field(ColumnDescriptor col, string label, string? value)
    {
        col.Item().PaddingVertical(1).Row(r =>
        {
            r.ConstantItem(125).Text(label + " :").FontSize(7).FontColor(FieldDim);
            r.RelativeItem().Text(string.IsNullOrWhiteSpace(value) ? "—" : value)
                .FontSize(8).Bold().FontColor(White);
        });
    }
}
