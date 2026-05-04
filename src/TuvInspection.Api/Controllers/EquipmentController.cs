using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Equipment;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Equipment;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/equipment")]
[Produces("application/json")]
public class EquipmentController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly EquipmentImportService _importer;

    public EquipmentController(IDispatcher dispatcher, EquipmentImportService importer)
    {
        _dispatcher = dispatcher;
        _importer = importer;
    }

    [HttpGet]
    public Task<PagedResult<EquipmentListItemDto>> List(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? equipmentTypeId,
        [FromQuery] AramcoCategoryDto? aramcoCategory,
        [FromQuery] EquipmentStatusDto? status,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListEquipmentQuery(
            clientId, equipmentTypeId, aramcoCategory, status, search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<EquipmentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetEquipmentByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator},{Roles.Inspector}")]
    public async Task<ActionResult<EquipmentDetailDto>> Create(
        [FromBody] CreateEquipmentRequest body, CancellationToken ct)
    {
        var dto = await _dispatcher.Send(new CreateEquipmentCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<EquipmentDetailDto> Update(Guid id, [FromBody] UpdateEquipmentRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateEquipmentCommand(id, body), ct);

    [HttpPost("import")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<EquipmentImportResult>> Import(
        [FromQuery] Guid clientId,
        IFormFile file,
        CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        await using var stream = file.OpenReadStream();
        var result = await _importer.Import(clientId, stream, ct);
        return Ok(result);
    }
}
