using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using TuvInspection.Application.Auth;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Auth;

namespace TuvInspection.Api.Controllers;

[ApiController]
[Route("api/auth")]
[Produces("application/json")]
public class AuthController : ControllerBase
{
    private readonly IDispatcher _dispatcher;

    public AuthController(IDispatcher dispatcher) => _dispatcher = dispatcher;

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _dispatcher.Send(new LoginCommand(request.UserName, request.Password, ip), ct);
        return result.Success
            ? Ok(result.Response)
            : Unauthorized(new ProblemDetails { Title = "Authentication failed", Detail = result.Error, Status = 401 });
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<LoginResponse>> Refresh(RefreshRequest request, CancellationToken ct)
    {
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _dispatcher.Send(new RefreshCommand(request.RefreshToken, ip), ct);
        return result.Success
            ? Ok(result.Response)
            : Unauthorized(new ProblemDetails { Title = "Refresh failed", Detail = result.Error, Status = 401 });
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<UserProfile>> Me(CancellationToken ct)
    {
        var profile = await _dispatcher.Query(new GetCurrentUserQuery(), ct);
        return profile is null ? Unauthorized() : Ok(profile);
    }
}
