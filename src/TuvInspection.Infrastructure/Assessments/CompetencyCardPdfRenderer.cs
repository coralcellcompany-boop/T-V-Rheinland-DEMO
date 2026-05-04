using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using TuvInspection.Contracts.Assessments;

namespace TuvInspection.Infrastructure.Assessments;

/// <summary>
/// Renders an operator Competency Card as a credit-card-shaped PDF (front + back) per
/// SRS §5.4.5. The renderer accepts an optional QR PNG so the back of the card embeds the
/// public verification QR. PII is shown in full because the card is issued to the named
/// candidate; for public/anonymous viewing, use <see cref="RenderPublic(CompetencyCardPublicViewDto, byte[])"/>.
/// </summary>
public sealed class CompetencyCardPdfRenderer
{
    static CompetencyCardPdfRenderer()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    private const float CardWidthMm = 105;   // ID-1 horizontal but slightly larger
    private const float CardHeightMm = 65;

    public byte[] Render(CompetencyCardDetailDto card, byte[]? qrPng)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(8);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor("#0f172a"));

                page.Content().Element(c => Front(c, card));
            });
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(8);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor("#0f172a"));

                page.Content().Element(c => Back(c, card, qrPng));
            });
        }).GeneratePdf();
    }

    public byte[] RenderPublic(CompetencyCardPublicViewDto card, byte[]? qrPng)
    {
        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A6);
                page.Margin(8);
                page.PageColor(Colors.White);
                page.DefaultTextStyle(t => t.FontSize(8).FontColor("#0f172a"));
                page.Content().Element(c => PublicSheet(c, card, qrPng));
            });
        }).GeneratePdf();
    }

    private static void Front(IContainer c, CompetencyCardDetailDto card)
    {
        c.Padding(4).Column(col =>
        {
            col.Item().Background("#0a3d62").Padding(10).Column(h =>
            {
                h.Item().Row(r =>
                {
                    r.RelativeItem().Column(t =>
                    {
                        t.Item().Text("TÜV RHEINLAND ARABIA").FontSize(11).Bold().FontColor("#fff");
                        t.Item().Text("Operator Competency Card").FontSize(8).FontColor("#bae6fd");
                    });
                    r.ConstantItem(64).AlignRight().Column(t =>
                    {
                        t.Item().Text(card.CardNo).FontSize(9).Bold().FontColor("#fff");
                    });
                });
            });

            col.Item().PaddingTop(8).Background("#f8fafc").Border(0.4f).BorderColor("#e5e9f2").Padding(10).Column(t =>
            {
                t.Item().Text(card.CandidateName).FontSize(13).Bold();
                t.Item().PaddingTop(2).Text($"Category: {CategoryLabel(card.Category)}").FontSize(9).FontColor("#0a3d62").Bold();
                t.Item().PaddingTop(8).Text($"ID No: {card.CandidateIdNumber}").FontSize(8).FontColor("#475569");
                if (!string.IsNullOrWhiteSpace(card.CandidateNationality))
                    t.Item().Text($"Nationality: {card.CandidateNationality}").FontSize(8).FontColor("#475569");
                t.Item().Text($"Sponsor: {card.ClientName}").FontSize(8).FontColor("#475569");
            });

            col.Item().PaddingTop(8).Row(r =>
            {
                r.RelativeItem().Column(x =>
                {
                    x.Item().Text("Issued").FontSize(7).FontColor("#94a3b8");
                    x.Item().Text(card.IssuedOn.ToString("dd MMM yyyy")).FontSize(9).Bold();
                });
                r.RelativeItem().Column(x =>
                {
                    x.Item().Text("Valid until").FontSize(7).FontColor("#94a3b8");
                    x.Item().Text(card.ValidUntil?.ToString("dd MMM yyyy") ?? "—").FontSize(9).Bold();
                });
                r.RelativeItem().Column(x =>
                {
                    x.Item().Text("Status").FontSize(7).FontColor("#94a3b8");
                    x.Item().Background(StatusBg(card.State)).Padding(3).AlignCenter()
                        .Text(card.State.ToString()).FontSize(8).Bold();
                });
            });

            col.Item().PaddingTop(10).Text("Front")
                .FontSize(7).FontColor("#cbd5e1").AlignCenter();
        });
    }

    private static void Back(IContainer c, CompetencyCardDetailDto card, byte[]? qrPng)
    {
        c.Padding(4).Column(col =>
        {
            col.Item().Row(r =>
            {
                r.RelativeItem().Column(t =>
                {
                    t.Item().Text("Verification").FontSize(8).FontColor("#94a3b8");
                    t.Item().Text(card.CardNo).FontSize(11).Bold();
                    t.Item().PaddingTop(4).Text("Scan the QR to verify validity online. The verification page shows masked PII.").FontSize(7).FontColor("#475569");
                    t.Item().PaddingTop(10).Text("Conditions of use").FontSize(8).Bold();
                    t.Item().Text("• Card holder must adhere to the operator scope above.").FontSize(7);
                    t.Item().Text("• This card remains property of TÜV Rheinland Arabia.").FontSize(7);
                    t.Item().Text("• Report loss / damage immediately.").FontSize(7);
                });
                r.ConstantItem(78).AlignTop().Column(qr =>
                {
                    if (qrPng is { Length: > 0 })
                        qr.Item().Width(78).Height(78).Image(qrPng).FitArea();
                    else
                        qr.Item().Width(78).Height(78).Background("#f1f5f9").AlignCenter().AlignMiddle().Text("QR").FontSize(10).FontColor("#94a3b8");
                });
            });

            col.Item().PaddingTop(10).LineHorizontal(0.5f).LineColor("#e5e9f2");
            col.Item().PaddingTop(4).Row(r =>
            {
                r.RelativeItem().Text($"Assessment: {card.AssessmentNo}").FontSize(7).FontColor("#475569");
                r.RelativeItem().AlignRight().Text("TÜV Rheinland Arabia LLC").FontSize(7).FontColor("#0a3d62").Bold();
            });

            col.Item().PaddingTop(10).Text("Back")
                .FontSize(7).FontColor("#cbd5e1").AlignCenter();
        });
    }

    private static void PublicSheet(IContainer c, CompetencyCardPublicViewDto card, byte[]? qrPng)
    {
        c.Padding(4).Column(col =>
        {
            col.Item().Background("#0a3d62").Padding(10).Column(h =>
            {
                h.Item().Text("TÜV RHEINLAND ARABIA").FontSize(11).Bold().FontColor("#fff");
                h.Item().Text("Operator Competency Card · Public verification").FontSize(8).FontColor("#bae6fd");
            });

            col.Item().PaddingTop(8).Background(card.IsValidNow ? "#ecfdf5" : "#fef2f2").Padding(10).Column(t =>
            {
                t.Item().Text(card.IsValidNow ? "VALID" : "NOT VALID")
                    .FontSize(14).Bold().FontColor(card.IsValidNow ? "#047857" : "#b91c1c");
                t.Item().Text($"Card {card.CardNo}  ·  {CategoryLabel(card.Category)}").FontSize(9);
            });

            col.Item().PaddingTop(8).Background("#f8fafc").Border(0.4f).BorderColor("#e5e9f2").Padding(10).Column(t =>
            {
                t.Item().Text("Holder (masked)").FontSize(7).FontColor("#94a3b8");
                t.Item().Text(card.CandidateNameMasked).FontSize(11).Bold();
                t.Item().Text($"ID: {card.CandidateIdMasked}").FontSize(8).FontColor("#475569");
                if (!string.IsNullOrWhiteSpace(card.ClientName))
                    t.Item().Text($"Sponsor: {card.ClientName}").FontSize(8).FontColor("#475569");
                t.Item().PaddingTop(6).Row(r =>
                {
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text("Issued").FontSize(7).FontColor("#94a3b8");
                        x.Item().Text(card.IssuedOn.ToString("dd MMM yyyy")).FontSize(9).Bold();
                    });
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text("Valid until").FontSize(7).FontColor("#94a3b8");
                        x.Item().Text(card.ValidUntil?.ToString("dd MMM yyyy") ?? "—").FontSize(9).Bold();
                    });
                    r.RelativeItem().Column(x =>
                    {
                        x.Item().Text("Status").FontSize(7).FontColor("#94a3b8");
                        x.Item().Text(card.State.ToString()).FontSize(9).Bold();
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

    private static string StatusBg(CompetencyCardStateDto s) => s switch
    {
        CompetencyCardStateDto.Issued    => "#dcfce7",
        CompetencyCardStateDto.Expired   => "#fee2e2",
        CompetencyCardStateDto.Revoked   => "#fee2e2",
        CompetencyCardStateDto.Suspended => "#fef3c7",
        CompetencyCardStateDto.Lost      => "#fef3c7",
        _ => "#f1f5f9",
    };

    private static string CategoryLabel(CompetencyCategoryDto c) => c switch
    {
        CompetencyCategoryDto.MobileCrane     => "Mobile Crane Operator",
        CompetencyCategoryDto.Forklift        => "Forklift Operator",
        CompetencyCategoryDto.Manlift         => "Manlift Operator",
        CompetencyCategoryDto.WheelLoader     => "Wheel Loader Operator",
        CompetencyCategoryDto.MewpTelehandler => "MEWP / Telehandler Operator",
        _ => c.ToString(),
    };
}
