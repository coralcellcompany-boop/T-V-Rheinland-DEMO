using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Dashboard;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/dashboard")]
[Produces("application/json")]
public class DashboardController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public DashboardController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("kpis")]
    public Task<DashboardKpisDto> Kpis([FromQuery] Guid? clientId, CancellationToken ct) =>
        _dispatcher.Query(new GetDashboardKpisQuery(clientId), ct);

    [HttpGet("activity")]
    public Task<IReadOnlyList<RecentActivityItemDto>> Activity(
        [FromQuery] int limit = 12, CancellationToken ct = default) =>
        _dispatcher.Query(new GetRecentActivityQuery(limit), ct);

    [HttpGet("inspector-analysis")]
    public Task<IReadOnlyList<InspectorAnalysisRowDto>> InspectorAnalysis(
        [FromQuery] int days = 90, CancellationToken ct = default) =>
        _dispatcher.Query(new GetInspectorAnalysisQuery(days), ct);
}
