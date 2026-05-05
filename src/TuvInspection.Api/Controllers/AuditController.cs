using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Auditing;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Auditing;
using TuvInspection.Contracts.Common;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

/// <summary>
/// Audit log browser. Manager-only — exposes the contents of the immutable hash-chained log.
/// Equipment-history is a thin wrapper that pre-filters by EntityName=Equipment and EntityId.
/// </summary>
[ApiController]
[Authorize(Roles = Roles.Manager)]
[Route("api/audit")]
[Produces("application/json")]
public class AuditController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public AuditController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<PagedResult<AuditLogRowDto>> List(
        [FromQuery] string? entityName,
        [FromQuery] string? entityId,
        [FromQuery] string? actorUserId,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        [FromQuery] string? search,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListAuditQuery(
            entityName, entityId, actorUserId, fromUtc, toUtc, search, page, pageSize), ct);
}

[ApiController]
[Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator},{Roles.Inspector}")]
[Route("api/equipment")]
[Produces("application/json")]
public class EquipmentHistoryController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public EquipmentHistoryController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("{id:guid}/history")]
    public Task<PagedResult<AuditLogRowDto>> History(Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken ct = default) =>
        _dispatcher.Query(new ListAuditQuery(
            "Equipment", id.ToString(), null, null, null, null, page, pageSize), ct);
}
