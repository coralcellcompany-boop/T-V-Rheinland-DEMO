using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.JobManagement;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/job-requests")]
[Produces("application/json")]
public class JobRequestsController : ControllerBase
{
    private readonly IDispatcher _d;
    public JobRequestsController(IDispatcher d) => _d = d;

    [HttpGet] public Task<PagedResult<JobRequestListItemDto>> List(
        [FromQuery] Guid? clientId, [FromQuery] JobRequestStatusDto? status,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _d.Query(new ListJobRequestsQuery(clientId, status, search, page, pageSize), ct);

    [HttpGet("{id:guid}")] public async Task<ActionResult<JobRequestDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _d.Query(new GetJobRequestByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public async Task<ActionResult<JobRequestDetailDto>> Create([FromBody] CreateJobRequestRequest body, CancellationToken ct)
    {
        var dto = await _d.Send(new CreateJobRequestCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobRequestDetailDto> Update(Guid id, [FromBody] UpdateJobRequestRequest body, CancellationToken ct) =>
        _d.Send(new UpdateJobRequestCommand(id, body), ct);

    [HttpPost("{id:guid}/accept")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobRequestDetailDto> Accept(Guid id, CancellationToken ct) =>
        _d.Send(new AcceptJobRequestCommand(id), ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobRequestDetailDto> Reject(Guid id, [FromBody] RejectJobRequestRequest body, CancellationToken ct) =>
        _d.Send(new RejectJobRequestCommand(id, body), ct);

    [HttpPost("{id:guid}/convert")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobOrderDetailDto> Convert(Guid id, CancellationToken ct) =>
        _d.Send(new ConvertJobRequestCommand(id), ct);
}

[ApiController]
[Authorize]
[Route("api/job-orders")]
[Produces("application/json")]
public class JobOrdersController : ControllerBase
{
    private readonly IDispatcher _d;
    public JobOrdersController(IDispatcher d) => _d = d;

    [HttpGet] public Task<PagedResult<JobOrderListItemDto>> List(
        [FromQuery] Guid? clientId, [FromQuery] JobOrderStatusDto? status,
        [FromQuery] string? search,
        [FromQuery] string? assignedInspectorId,
        [FromQuery] bool mineOnly = false,
        [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _d.Query(new ListJobOrdersQuery(
            clientId, status, search, assignedInspectorId, mineOnly, page, pageSize), ct);

    [HttpGet("{id:guid}")] public async Task<ActionResult<JobOrderDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _d.Query(new GetJobOrderByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public async Task<ActionResult<JobOrderDetailDto>> Create([FromBody] CreateJobOrderRequest body, CancellationToken ct)
    {
        var dto = await _d.Send(new CreateJobOrderCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobOrderDetailDto> Update(Guid id, [FromBody] UpdateJobOrderRequest body, CancellationToken ct) =>
        _d.Send(new UpdateJobOrderCommand(id, body), ct);

    /// <summary>Move an Open job order into InProgress. Inspector (assigned) or staff.</summary>
    [HttpPost("{id:guid}/begin")]
    public Task<JobOrderDetailDto> Begin(Guid id, CancellationToken ct) =>
        _d.Send(new BeginJobOrderCommand(id), ct);

    /// <summary>Mark a job order Completed. Inspector (assigned) or staff.</summary>
    [HttpPost("{id:guid}/complete")]
    public Task<JobOrderDetailDto> Complete(Guid id, CancellationToken ct) =>
        _d.Send(new CompleteJobOrderCommand(id), ct);

    /// <summary>Cancel a job order. Manager/Coordinator only.</summary>
    [HttpPost("{id:guid}/cancel")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobOrderDetailDto> Cancel(Guid id, CancellationToken ct) =>
        _d.Send(new CancelJobOrderCommand(id), ct);

    /// <summary>
    /// Auto-assign the least-loaded eligible inspector. Picks active Inspector role users whose
    /// client scope covers this Job Order's client, ordered by in-flight certificate count.
    /// Manager/Coordinator only.
    /// </summary>
    [HttpPost("{id:guid}/auto-assign-inspector")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<JobOrderDetailDto> AutoAssignInspector(Guid id, CancellationToken ct) =>
        _d.Send(new AutoAssignJobOrderInspectorCommand(id), ct);
}

[ApiController]
[Authorize]
[Route("api/dwr")]
[Produces("application/json")]
public class DwrController : ControllerBase
{
    private readonly IDispatcher _d;
    public DwrController(IDispatcher d) => _d = d;

    [HttpGet] public Task<PagedResult<DwrListItemDto>> List(
        [FromQuery] Guid? jobOrderId, [FromQuery] string? inspectorId,
        [FromQuery] DwrStatusDto? status,
        [FromQuery] DateOnly? dateFrom, [FromQuery] DateOnly? dateTo,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _d.Query(new ListDwrQuery(jobOrderId, inspectorId, status, dateFrom, dateTo, search, page, pageSize), ct);

    [HttpGet("{id:guid}")] public async Task<ActionResult<DwrDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _d.Query(new GetDwrByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<DwrDetailDto>> Create([FromBody] CreateDwrRequest body, CancellationToken ct)
    {
        var dto = await _d.Send(new CreateDwrCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")] public Task<DwrDetailDto> Update(Guid id, [FromBody] UpdateDwrRequest body, CancellationToken ct) =>
        _d.Send(new UpdateDwrCommand(id, body), ct);

    [HttpPost("{id:guid}/submit")] public Task<DwrDetailDto> Submit(Guid id, CancellationToken ct) =>
        _d.Send(new SubmitDwrCommand(id), ct);

    [HttpPost("{id:guid}/approve")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<DwrDetailDto> Approve(Guid id, CancellationToken ct) => _d.Send(new ApproveDwrCommand(id), ct);

    [HttpPost("{id:guid}/reject")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<DwrDetailDto> Reject(Guid id, [FromBody] DwrRejectRequest body, CancellationToken ct) =>
        _d.Send(new RejectDwrCommand(id, body), ct);
}

[ApiController]
[Authorize]
[Route("api/surveys")]
[Produces("application/json")]
public class SurveysController : ControllerBase
{
    private readonly IDispatcher _d;
    public SurveysController(IDispatcher d) => _d = d;

    [HttpGet] public Task<PagedResult<SurveyListItemDto>> List(
        [FromQuery] Guid? clientId, [FromQuery] SurveyStatusDto? status,
        [FromQuery] string? search, [FromQuery] int page = 1, [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _d.Query(new ListSurveysQuery(clientId, status, search, page, pageSize), ct);

    [HttpGet("{id:guid}")] public async Task<ActionResult<SurveyDetailDto>> GetById(Guid id, CancellationToken ct)
    {
        var dto = await _d.Query(new GetSurveyByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost] public async Task<ActionResult<SurveyDetailDto>> Create([FromBody] CreateSurveyRequest body, CancellationToken ct)
    {
        var dto = await _d.Send(new CreateSurveyCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id:guid}")] public Task<SurveyDetailDto> Update(Guid id, [FromBody] UpdateSurveyRequest body, CancellationToken ct) =>
        _d.Send(new UpdateSurveyCommand(id, body), ct);

    [HttpPost("{id:guid}/submit")] public Task<SurveyDetailDto> Submit(Guid id, CancellationToken ct) =>
        _d.Send(new SubmitSurveyCommand(id), ct);
}
