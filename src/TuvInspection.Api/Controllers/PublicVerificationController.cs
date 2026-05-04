using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TuvInspection.Application.Assessments;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Stickers;
using TuvInspection.Contracts.Assessments;
using TuvInspection.Contracts.Stickers;
using TuvInspection.Infrastructure.Stickers;

namespace TuvInspection.Api.Controllers;

/// <summary>
/// Anonymous, mobile-friendly endpoints for QR-driven public sticker verification.
/// Returns only public-safe data (company name + masked equipment ID + validity).
/// </summary>
[ApiController]
[AllowAnonymous]
[Route("api/public")]
public class PublicVerificationController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly QrCodeService _qr;
    private readonly string _publicBaseUrl;

    public PublicVerificationController(IDispatcher dispatcher, QrCodeService qr, IConfiguration config)
    {
        _dispatcher = dispatcher;
        _qr = qr;
        _publicBaseUrl = config["Public:BaseUrl"]?.TrimEnd('/') ?? "http://localhost:4201";
    }

    [HttpGet("stickers/{stickerNo}")]
    [Produces("application/json")]
    public async Task<ActionResult<StickerPublicViewDto>> Verify(string stickerNo, CancellationToken ct)
    {
        var view = await _dispatcher.Query(new GetStickerPublicViewQuery(stickerNo), ct);
        return view is null ? NotFound() : Ok(view);
    }

    /// <summary>QR PNG that resolves to <c>{publicBaseUrl}/verify/{stickerNo}</c>.</summary>
    [HttpGet("qr/{stickerNo}.png")]
    public IActionResult Qr(string stickerNo)
    {
        if (string.IsNullOrWhiteSpace(stickerNo)) return BadRequest();
        var safe = stickerNo.Trim().ToUpperInvariant();
        var url = $"{_publicBaseUrl}/verify/{safe}";
        var bytes = _qr.PngFor(url);
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, "image/png", $"{safe}.png");
    }

    [HttpGet("cards/{cardNo}")]
    [Produces("application/json")]
    public async Task<ActionResult<CompetencyCardPublicViewDto>> VerifyCard(string cardNo, CancellationToken ct)
    {
        var view = await _dispatcher.Query(new GetCompetencyCardPublicViewQuery(cardNo), ct);
        return view is null ? NotFound() : Ok(view);
    }

    [HttpGet("qr/cards/{cardNo}.png")]
    public IActionResult QrCard(string cardNo)
    {
        if (string.IsNullOrWhiteSpace(cardNo)) return BadRequest();
        var safe = cardNo.Trim().ToUpperInvariant();
        var url = $"{_publicBaseUrl}/verify-card/{safe}";
        var bytes = _qr.PngFor(url);
        Response.Headers.CacheControl = "public, max-age=86400";
        return File(bytes, "image/png", $"{safe}.png");
    }
}
