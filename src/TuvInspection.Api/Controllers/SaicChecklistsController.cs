using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/saic-checklists")]
[Produces("application/json")]
public class SaicChecklistsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public SaicChecklistsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    /// <summary>Resolve the SAIC checklist for an equipment selection. 204 when unmapped.</summary>
    [HttpGet("resolve")]
    public async Task<ActionResult<SaicChecklistDto>> Resolve(
        [FromQuery] string category, [FromQuery] string equipmentType, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new ResolveSaicChecklistQuery(category, equipmentType), ct);
        return dto is null ? NoContent() : Ok(dto);
    }
}
