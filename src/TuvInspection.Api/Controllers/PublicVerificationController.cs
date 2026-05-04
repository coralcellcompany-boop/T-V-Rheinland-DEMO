using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TuvInspection.Application.Assessments;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Stickers;
using TuvInspection.Contracts.Assessments;
using TuvInspection.Contracts.Stickers;
using TuvInspection.Infrastructure.Assessments;
using TuvInspection.Infrastructure.Stickers;

namespace TuvInspection.Api.Controllers;

/// <summary>
/// Anonymous, mobile-friendly endpoints for QR-driven public verification.
/// QR codes encode the URL of the public verification PDF endpoint, so a phone scan
/// opens the verification sheet directly without going through the SPA.
/// All public PDFs and JSON omit PII (full names, full equipment IDs).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicVerificationController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly QrCodeService _qr;
    private readonly StickerPdfRenderer _stickerPdf;
    private readonly CompetencyCardPdfRenderer _cardPdf;
    private readonly string _apiBaseUrl;

    public PublicVerificationController(
        IDispatcher dispatcher,
        QrCodeService qr,
        StickerPdfRenderer stickerPdf,
        CompetencyCardPdfRenderer cardPdf,
        IConfiguration config)
    {
        _dispatcher = dispatcher;
        _qr = qr;
        _stickerPdf = stickerPdf;
        _cardPdf = cardPdf;
        _apiBaseUrl = config["Public:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5282";
    }

    // ─────────── JSON verification (used by the SPA verify pages) ───────────

    [HttpGet("stickers/{stickerNo}")]
    [Produces("application/json")]
    public async Task<ActionResult<StickerPublicViewDto>> Verify(string stickerNo, CancellationToken ct)
    {
        var view = await _dispatcher.Query(new GetStickerPublicViewQuery(stickerNo), ct);
        return view is null ? NotFound() : Ok(view);
    }

    [HttpGet("cards/{cardNo}")]
    [Produces("application/json")]
    public async Task<ActionResult<CompetencyCardPublicViewDto>> VerifyCard(string cardNo, CancellationToken ct)
    {
        var view = await _dispatcher.Query(new GetCompetencyCardPublicViewQuery(cardNo), ct);
        return view is null ? NotFound() : Ok(view);
    }

    // ─────────── Public PDF (the QR scan target) ───────────

    [HttpGet("stickers/{stickerNo}.pdf")]
    public async Task<IActionResult> StickerPdf(string stickerNo, CancellationToken ct)
    {
        var view = await _dispatcher.Query(new GetStickerPublicViewQuery(stickerNo), ct);
        if (view is null) return NotFound();

        var qrUrl = StickerPdfUrl(view.StickerNo);
        var qr = _qr.PngFor(qrUrl);
        var bytes = _stickerPdf.RenderPublic(view, qr);
        Response.Headers.CacheControl = "public, max-age=300";
        return File(bytes, "application/pdf", $"{view.StickerNo}.pdf");
    }

    [HttpGet("cards/{cardNo}.pdf")]
    public async Task<IActionResult> CardPdf(string cardNo, CancellationToken ct)
    {
        var view = await _dispatcher.Query(new GetCompetencyCardPublicViewQuery(cardNo), ct);
        if (view is null) return NotFound();

        var qrUrl = CardPdfUrl(view.CardNo);
        var qr = _qr.PngFor(qrUrl);
        var bytes = _cardPdf.RenderPublic(view, qr);
        Response.Headers.CacheControl = "public, max-age=300";
        return File(bytes, "application/pdf", $"{view.CardNo}.pdf");
    }

    // ─────────── QR PNGs (encoded with the public PDF URL) ───────────

    /// <summary>QR PNG that resolves to the sticker's public PDF — scanning it opens the verification sheet.</summary>
    [HttpGet("qr/{stickerNo}.png")]
    public IActionResult Qr(string stickerNo)
    {
        if (string.IsNullOrWhiteSpace(stickerNo)) return BadRequest();
        var safe = stickerNo.Trim().ToUpperInvariant();
        var bytes = _qr.PngFor(StickerPdfUrl(safe));
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, "image/png", $"{safe}.png");
    }

    /// <summary>QR PNG that resolves to the card's public PDF.</summary>
    [HttpGet("qr/cards/{cardNo}.png")]
    public IActionResult QrCard(string cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return BadRequest();
        var safe = cardNo.Trim().ToUpperInvariant();
        var bytes = _qr.PngFor(CardPdfUrl(safe));
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, "image/png", $"{safe}.png");
    }

    private string StickerPdfUrl(string no) =>
        $"{_apiBaseUrl}/api/public/stickers/{Uri.EscapeDataString(no)}.pdf";

    private string CardPdfUrl(string no) =>
        $"{_apiBaseUrl}/api/public/cards/{Uri.EscapeDataString(no)}.pdf";
}
