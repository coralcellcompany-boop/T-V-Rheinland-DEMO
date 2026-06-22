using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Application.Users;
using TuvInspection.Contracts.Users;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Authorize(Roles = Roles.Manager)]
[Route("api/admin/users")]
[Produces("application/json")]
public class UsersController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public UsersController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet]
    public Task<IReadOnlyList<UserListItemDto>> List([FromQuery] string? search, CancellationToken ct) =>
        _dispatcher.Query(new ListUsersQuery(search), ct);

    [HttpGet("{id}")]
    public async Task<ActionResult<UserListItemDto>> GetById(string id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetUserByIdQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPost]
    public async Task<ActionResult<UserListItemDto>> Create(
        [FromBody] CreateUserRequest body, CancellationToken ct)
    {
        var dto = await _dispatcher.Send(new CreateUserCommand(body), ct);
        return CreatedAtAction(nameof(GetById), new { id = dto.Id }, dto);
    }

    [HttpPut("{id}")]
    public Task<UserListItemDto> Update(string id, [FromBody] UpdateUserRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateUserCommand(id, body), ct);

    [HttpPost("{id}/reset-password")]
    public async Task<IActionResult> ResetPassword(string id, [FromBody] ResetPasswordRequest body,
        CancellationToken ct)
    {
        await _dispatcher.Send(new ResetUserPasswordCommand(id, body), ct);
        return NoContent();
    }

    [HttpGet("roles")]
    public ActionResult<IReadOnlyList<string>> GetRoles() => Ok(TuvInspection.Domain.Identity.Roles.All);

    [HttpGet("{id}/license")]
    public async Task<ActionResult<UserLicenseDto>> GetLicense(string id, CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetUserLicenseQuery(id), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("{id}/license")]
    public Task<UserLicenseDto> UpdateLicense(string id,
        [FromBody] UpdateUserLicenseRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateUserLicenseCommand(id, body), ct);
}

/// <summary>Self-service profile endpoints — available to any authenticated user.</summary>
[ApiController]
[Authorize]
[Route("api/profile")]
[Produces("application/json")]
public class ProfileController : ControllerBase
{
    private readonly IDispatcher _dispatcher;
    public ProfileController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpGet("me")]
    public async Task<ActionResult<ProfileDto>> Me(CancellationToken ct)
    {
        var dto = await _dispatcher.Query(new GetMyProfileQuery(), ct);
        return dto is null ? NotFound() : Ok(dto);
    }

    [HttpPut("signature")]
    public Task<ProfileDto> UpdateSignature(
        [FromBody] UpdateProfileSignatureRequest body, CancellationToken ct) =>
        _dispatcher.Send(new UpdateMySignatureCommand(body), ct);
}
