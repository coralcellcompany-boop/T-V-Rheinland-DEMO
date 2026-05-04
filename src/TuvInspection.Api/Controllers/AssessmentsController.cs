using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Assessments;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Assessments;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/assessments")]
[Produces("application/json")]
public class AssessmentsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public AssessmentsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<PagedResult<AssessmentListItemDto>> List(
        [FromQuery] Guid? candidateId,
        [FromQuery] Guid? clientId,
        [FromQuery] AssessmentStateDto? state,
        [FromQuery] CompetencyCategoryDto? category,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListAssessmentsQuery(
            candidateId, clientId, state, category, search, page, pageSize), ct);

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<AssessmentDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetAssessmentByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator},{Roles.Inspector},{Roles.TechReviewer}")]
    public async Task<ActionResult<AssessmentDetailDto>> Create(
        [FromBody] CreateAssessmentRequest body, CancellationToken ct)
    {
        var dto = await _dispatcher.Send(new CreateAssessmentCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator},{Roles.Inspector},{Roles.TechReviewer}")]
    public Task<AssessmentDetailDto> Update(Guid id, [FromBody] UpdateAssessmentRequest body,
        CancellationToken ct) =>
        _dispatcher.Send(new UpdateAssessmentCommand(id, body), ct);

    [HttpPost("{id:guid}/transitions/{trigger}")]
    public Task<AssessmentDetailDto> Transition(
        Guid id, string trigger,
        [FromBody] AssessmentTransitionRequest? body, CancellationToken ct)
    {
        if (!Enum.TryParse<AssessmentTriggerDto>(trigger, ignoreCase: true, out var t))
            throw new ArgumentException($"Unknown trigger '{trigger}'.");
        return _dispatcher.Send(new FireAssessmentTriggerCommand(id, t, body?.Comments), ct);
    }
}

[ApiController]
[Authorize]
[Route("api/cards")]
[Produces("application/json")]
public class CompetencyCardsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public CompetencyCardsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<PagedResult<CompetencyCardListItemDto>> List(
        [FromQuery] Guid? clientId,
        [FromQuery] Guid? candidateId,
        [FromQuery] CompetencyCardStateDto? state,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListCompetencyCardsQuery(
            clientId, candidateId, state, search, page, pageSize), ct);
}
