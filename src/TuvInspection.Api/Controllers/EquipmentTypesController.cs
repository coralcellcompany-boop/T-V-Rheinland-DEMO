using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Equipment;
using TuvInspection.Contracts.Equipment;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/equipment-types")]
[Produces("application/json")]
public class EquipmentTypesController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public EquipmentTypesController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<IReadOnlyList<EquipmentTypeDto>> List(CancellationToken ct) =>
        _dispatcher.Query(new ListEquipmentTypesQuery(), ct);
}
