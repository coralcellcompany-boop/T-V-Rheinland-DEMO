using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Reports;
using TuvInspection.Contracts.Reports;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Reports;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
[Route("api/reports")]
[Produces("application/json")]
public class ReportsController : ControllerBase
{
    private readonly IDispatcher _d;
    private readonly AramcoWeeklyExporter _exporter;
    public ReportsController(IDispatcher d, AramcoWeeklyExporter exporter) { _d = d; _exporter = exporter; }

    [HttpGet("monthly-stats")]
    public Task<IReadOnlyList<MonthlyStatsRowDto>> Monthly([FromQuery] int months = 6, CancellationToken ct = default) =>
        _d.Query(new GetMonthlyStatsQuery(months), ct);

    [HttpGet("inspector-productivity")]
    public Task<IReadOnlyList<InspectorProductivityRowDto>> InspectorProductivity(
        [FromQuery] int days = 30, CancellationToken ct = default) =>
        _d.Query(new GetInspectorProductivityQuery(days), ct);

    [HttpGet("due-soon")]
    public Task<IReadOnlyList<DueSoonRowDto>> DueSoon([FromQuery] int days = 30, CancellationToken ct = default) =>
        _d.Query(new GetDueSoonQuery(days), ct);

    [HttpGet("overdue")]
    public Task<IReadOnlyList<OverdueRowDto>> Overdue(CancellationToken ct = default) =>
        _d.Query(new GetOverdueQuery(), ct);

    [HttpGet("aramco-weekly")]
    public async Task<IActionResult> AramcoWeekly(
        [FromQuery] DateOnly? cutoff,
        [FromQuery] Guid? clientId,
        CancellationToken ct = default)
    {
        var date = cutoff ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var (bytes, fileName) = await _exporter.Generate(date, clientId, ct);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            fileName);
    }
}
