using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Stickers;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Stickers;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
[Route("api/stickers")]
[Produces("application/json")]
public class StickersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public StickersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<PagedResult<StickerListItemDto>> List(
        [FromQuery] StickerStateDto? state,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListStickersQuery(state, search, page, pageSize), ct);

    [HttpGet("stock-summary")]
    public Task<StickerStockSummaryDto> StockSummary(CancellationToken ct) =>
        _dispatcher.Query(new GetStickerStockSummaryQuery(), ct);

    [HttpPost("procure")]
    [Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Procure([FromBody] ProcureStockRequest body, CancellationToken ct)
    {
        var n = await _dispatcher.Send(new ProcureStickerStockCommand(body.Count), ct);
        return Ok(new { added = n });
    }

    [HttpPost("{id:guid}/void")]
    [Authorize(Roles = Roles.Manager)]
    public Task<StickerListItemDto> Void(Guid id, [FromBody] VoidStickerRequest body, CancellationToken ct) =>
        _dispatcher.Send(new VoidStickerCommand(id, body.Reason), ct);
}
