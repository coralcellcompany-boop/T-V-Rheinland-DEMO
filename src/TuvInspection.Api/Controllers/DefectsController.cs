using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Equipment;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/defects")]
[Produces("application/json")]
public class DefectsController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public DefectsController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<IReadOnlyList<DefectCodeDto>> List(
        [FromQuery] Guid? equipmentTypeId,
        [FromQuery] bool includeInactive = false,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListDefectCodesQuery(equipmentTypeId, includeInactive), ct);

    [HttpPost]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<DefectCodeDto> Create([FromBody] CreateDefectCodeRequest body, CancellationToken ct) =>
        _dispatcher.Send(new CreateDefectCodeCommand(body), ct);

    [HttpPut("{id:guid}")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<DefectCodeDto> Update(Guid id, [FromBody] UpdateDefectCodeRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateDefectCodeCommand(id, body), ct);
}
