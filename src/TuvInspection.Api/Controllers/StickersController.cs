using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Stickers;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Stickers;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Stickers;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/stickers")]
[Produces("application/json")]
public class StickersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly QrCodeService _qr;
    private readonly StickerPdfRenderer _pdf;
    private readonly IConfiguration _config;

    public StickersController(IDispatcher dispatcher, QrCodeService qr, StickerPdfRenderer pdf, IConfiguration config)
    {
        _dispatcher = dispatcher;
        _qr = qr;
        _pdf = pdf;
        _config = config;
    }

    [HttpGet]
    public Task<PagedResult<StickerListItemDto>> List(
        [FromQuery] StickerStateDto? state,
        [FromQuery] StickerColorDto? color,
        [FromQuery] string? assignedToInspectorId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListStickersQuery(
            state, color, assignedToInspectorId, search, page, pageSize), ct);

    [HttpGet("stock-summary")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<StickerStockSummaryDto> StockSummary(CancellationToken ct) =>
        _dispatcher.Query(new GetStickerStockSummaryQuery(), ct);

    [HttpPost("procure")]
    [Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Procure([FromBody] ProcureStockRequest body, CancellationToken ct)
    {
        var n = await _dispatcher.Send(new ProcureStickerStockCommand(body.Count, body.Color), ct);
        return Ok(new { added = n });
    }

    [HttpPost("{id:guid}/void")]
    [Authorize(Roles = Roles.Manager)]
    public Task<StickerListItemDto> Void(Guid id, [FromBody] VoidStickerRequest body, CancellationToken ct) =>
        _dispatcher.Send(new VoidStickerCommand(id, body.Reason), ct);

    [HttpPost("assign")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public async Task<IActionResult> Assign([FromBody] AssignStickersRequest body, CancellationToken ct)
    {
        var n = await _dispatcher.Send(new AssignStickersToInspectorCommand(
            body.InspectorUserId, body.Color, body.Count, null), ct);
        return Ok(new { assigned = n });
    }

    /// <summary>Print a batch of unallocated stickers — one A4 page with up to 24 stickers,
    /// each with its public-PDF QR. Manager/Coordinator only.</summary>
    [HttpGet("print-batch")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public async Task<IActionResult> PrintBatch(
        [FromQuery] StickerStateDto? state,
        [FromQuery] StickerColorDto? color,
        [FromQuery] int max = 24,
        CancellationToken ct = default)
    {
        var clamped = Math.Clamp(max, 1, 96);
        var page = await _dispatcher.Query(
            new ListStickersQuery(state ?? StickerStateDto.Unallocated, color, null, null, 1, clamped), ct);
        var apiBase = _config["Public:ApiBaseUrl"]?.TrimEnd('/') ?? "http://localhost:5282";
        var rows = page.Items.Select(s =>
        {
            var url = $"{apiBase}/api/public/stickers/{Uri.EscapeDataString(s.StickerNo)}.pdf";
            return (s.StickerNo, _qr.PngFor(url));
        }).ToList();
        var bytes = _pdf.RenderPrintBatch(rows);
        return File(bytes, "application/pdf", $"sticker-batch-{DateTime.UtcNow:yyyyMMddHHmm}.pdf");
    }
}

[ApiController]
[Authorize]
[Route("api/sticker-requests")]
[Produces("application/json")]
public class StickerRequestsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public StickerRequestsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<PagedResult<StickerRequestDto>> List(
        [FromQuery] StickerRequestStateDto? state,
        [FromQuery] string? inspectorUserId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListStickerRequestsQuery(state, inspectorUserId, page, pageSize), ct);

    [HttpPost]
    public Task<StickerRequestDto> Create([FromBody] CreateStickerRequest body, CancellationToken ct) =>
        _dispatcher.Send(new CreateStickerRequestCommand(body), ct);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<StickerRequestDto> Approve(Guid id, [FromBody] DecisionCommentsBody? body, CancellationToken ct) =>
        _dispatcher.Send(new ApproveStickerRequestCommand(id, body?.Comments), ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<StickerRequestDto> Reject(Guid id, [FromBody] RejectStickerRequestBody body, CancellationToken ct) =>
        _dispatcher.Send(new RejectStickerRequestCommand(id, body.Reason), ct);

    [HttpPost("{id:guid}/cancel")]
    public Task<StickerRequestDto> Cancel(Guid id, CancellationToken ct) =>
        _dispatcher.Send(new CancelStickerRequestCommand(id), ct);
}
