using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Users;
using TuvInspection.Contracts.Users;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

/// <summary>
/// Read-only user lookup endpoints used by features that need to pick a user
/// (sticker assignment, job-order assignment) without granting full admin access.
/// </summary>
[ApiController]
[Authorize]
[Route("api/users")]
[Produces("application/json")]
public class UsersLookupController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public UsersLookupController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("inspectors")]
    [Authorize(Roles = $"{Roles.Manager},{Roles.Coordinator}")]
    public Task<IReadOnlyList<InspectorLookupDto>> Inspectors(CancellationToken ct) =>
        _dispatcher.Query(new ListInspectorsQuery(), ct);
}
