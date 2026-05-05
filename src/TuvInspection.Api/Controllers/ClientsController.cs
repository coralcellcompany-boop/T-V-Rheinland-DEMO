using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Clients;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Clients;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Clients;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/clients")]
[Produces("application/json")]
public class ClientsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly ClientImportService _importer;
    public ClientsController(IDispatcher dispatcher, ClientImportService importer)
    {
        _dispatcher = dispatcher;
        _importer = importer;
    }

    [HttpGet]
    public Task<PagedResult<ClientListItemDto>> List(
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListClientsQuery(search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ClientDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var c = await _dispatcher.Query(new GetClientByIdQuery(id), ct);
        return c is null ? NotFound() : Ok(c);
    }

    [HttpPost]
    [Authorize(Roles = Roles.Manager)]
    public async Task<ActionResult<ClientDetailDto>> Create(
        [FromBody] CreateClientRequest body, CancellationToken ct)
    {
        var created = await _dispatcher.Send(new CreateClientCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = created.Id }, created);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = Roles.Manager)]
    public Task<ClientDetailDto> Update(Guid id, [FromBody] UpdateClientRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateClientCommand(id, body), ct);

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = Roles.Manager)]
    public async Task<IActionResult> Delete(Guid id, CancellationToken ct)
    {
        await _dispatcher.Send(new DeleteClientCommand(id), ct);
        return NoContent();
    }

    [HttpPost("import")]
    [Authorize(Roles = Roles.Manager)]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<EquipmentImportResult>> Import(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        await using var stream = file.OpenReadStream();
        var result = await _importer.Import(stream, ct);
        return Ok(result);
    }
}
