using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Assessments;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Assessments;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Assessments;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/candidates")]
[Produces("application/json")]
public class CandidatesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    private readonly CandidateImportService _importer;
    public CandidatesController(IDispatcher dispatcher, CandidateImportService importer)
    {
        _dispatcher = dispatcher;
        _importer = importer;
    }

    [HttpGet]
    public Task<PagedResult<CandidateListItemDto>> List(
        [FromQuery] Guid? clientId,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListCandidatesQuery(clientId, search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<CandidateDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetCandidateByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public async Task<ActionResult<CandidateDetailDto>> Create(
        [FromBody] CreateCandidateRequest body, CancellationToken ct)
    {
        var dto = await _dispatcher.Send(new CreateCandidateCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<CandidateDetailDto> Update(Guid id, [FromBody] UpdateCandidateRequest body,
        CancellationToken ct) =>
        _dispatcher.Send(new UpdateCandidateCommand(id, body), ct);

    [HttpPost("import")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    [RequestSizeLimit(10 * 1024 * 1024)]
    public async Task<ActionResult<EquipmentImportResult>> Import(
        [FromQuery] Guid clientId, IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0) return BadRequest("No file uploaded.");
        await using var stream = file.OpenReadStream();
        var result = await _importer.Import(clientId, stream, ct);
        return Ok(result);
    }
}
