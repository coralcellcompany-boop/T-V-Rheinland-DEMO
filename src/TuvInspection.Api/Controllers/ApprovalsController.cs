using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Certificates;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Certificates;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/approvals")]
[Produces("application/json")]
public class ApprovalsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public ApprovalsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("counts")]
    public Task<ApprovalQueueCountsDto> Counts(CancellationToken ct) =>
        _dispatcher.Query(new GetApprovalQueueCountsQuery(), ct);

    [HttpGet("{bucket}")]
    public Task<PagedResult<CertificateListItemDto>> List(
        string bucket,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListApprovalQueueQuery(
            bucket?.ToLowerInvariant() ?? "pending", page, pageSize), ct);
}
