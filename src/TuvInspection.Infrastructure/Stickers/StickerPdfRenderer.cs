using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.Stickers;

namespace TuvInspection.Infrastructure.Stickers;

/// <summary>
/// Sticker-related PDFs:
///  - <see cref="RenderPublic"/>: a single-page A6 verification sheet shown when a QR is scanned.
///  - <see cref="RenderPrintBatch"/>: an A4 sheet of stickers (3 across × 8 rows = 24/page) for
///    printing onto adhesive label stock per SRS §5.2.
/// All PII is omitted; the equipment ID is shown only as the last 6 characters.
/// </summary>
public sealed class StickerPdfRenderer
{
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
                page.Margin(8);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor("#0f172a"));
                page.Content().Element(el => Sheet(el, v, qrPng));
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
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor("#0f172a"));
                page.Content().Table(t =>
                {
                    t.ColumnsDefinition(c => { c.RelativeColumn(); c.RelativeColumn(); c.RelativeColumn(); });
                    foreach (var r in rows)
                    {
                        t.Cell().Padding(4).Border(0.5f).BorderColor("#cbd5e1").Padding(8).Column(col =>
                        {
                            col.Item().Row(row =>
                            {
                                row.RelativeItem().Column(meta =>
                                {
                                    meta.Item().Text("TÜV BLUE").FontSize(8).Bold().FontColor("#0a3d62");
                                    meta.Item().Text(r.StickerNo).FontSize(11).Bold();
                                    meta.Item().PaddingTop(2).Text("Inspection Sticker").FontSize(7).FontColor("#475569");
                                });
                                row.ConstantItem(56).AlignRight().Image(r.QrPng).FitArea();
                            });
                            col.Item().PaddingTop(2).Text("Scan to verify").FontSize(6).FontColor("#94a3b8").AlignCenter();
                        });
                    }
                });
            });
        }).GeneratePdf();
    }

    private static void Sheet(IContainer c, StickerPublicViewDto v, byte[]? qrPng)
    {
        c.Padding(4).Column(col =>
        {
            col.Item().Background("#0a3d62").Padding(10).Column(h =>
            {
                h.Item().Text("TÜV RHEINLAND ARABIA").FontSize(11).Bold().FontColor("#fff");
                h.Item().Text("Inspection Sticker · Public verification").FontSize(8).FontColor("#bae6fd");
            });

            col.Item().PaddingTop(8).Background(v.IsValidNow ? "#ecfdf5" : "#fef2f2").Padding(10).Column(t =>
            {
                t.Item().Text(v.IsValidNow ? "VALID" : "NOT VALID")
                    .FontSize(14).Bold().FontColor(v.IsValidNow ? "#047857" : "#b91c1c");
                t.Item().Text($"Sticker {v.StickerNo}").FontSize(9);
            });

            col.Item().PaddingTop(8).Background("#f8fafc").Border(0.4f).BorderColor("#e5e9f2").Padding(10).Column(t =>
            {
                t.Item().Text("Equipment (masked)").FontSize(7).FontColor("#94a3b8");
                t.Item().Text(v.EquipmentIdNo ?? "—").FontSize(11).Bold();
                if (!string.IsNullOrWhiteSpace(v.EquipmentTypeName))
                    t.Item().Text($"Type: {v.EquipmentTypeName}").FontSize(8).FontColor("#475569");
                if (!string.IsNullOrWhiteSpace(v.AramcoCategory))
                    t.Item().Text($"Aramco: {v.AramcoCategory}").FontSize(8).FontColor("#475569");
                if (!string.IsNullOrWhiteSpace(v.ClientName))
                    t.Item().Text($"Operator: {v.ClientName}").FontSize(8).FontColor("#475569");
                t.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text("Issued").FontSize(7).FontColor("#94a3b8");
                        x.Item().Text(v.IssuedAtUtc?.ToString("dd MMM yyyy") ?? "—").FontSize(9).Bold();
                    });
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text("Valid until").FontSize(7).FontColor("#94a3b8");
                        x.Item().Text(v.ValidUntil?.ToString("dd MMM yyyy") ?? "—").FontSize(9).Bold();
                    });
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text("Status").FontSize(7).FontColor("#94a3b8");
                        x.Item().Text(v.State.ToString()).FontSize(9).Bold();
                    });
                });
            });

            col.Item().PaddingTop(8).Row(r =>
            {
                r.RelativeItem().Text("This document is auto-generated from a QR scan and shows masked information.").FontSize(7).FontColor("#94a3b8");
                if (qrPng is { Length: > 0 })
                    r.ConstantItem(56).AlignRight().Image(qrPng);
            });
        });
    }
}
