using Microsoft.AspNetCore.Identity;
using TuvInspection.Application.Auth;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Auth;

namespace TuvInspection.Infrastructure.Identity;

public sealed class LoginCommandHandler : ICommandHandler<LoginCommand, LoginResult>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly SignInManager<ApplicationUser> _signin;
    private readonly IJwtTokenService _tokens;

    public LoginCommandHandler(
        UserManager<ApplicationUser> users,
        SignInManager<ApplicationUser> signin,
        IJwtTokenService tokens)
    {
        _users = users;
        _signin = signin;
        _tokens = tokens;
    }

    public async Task<LoginResult> Handle(LoginCommand command, CancellationToken ct)
    {
        var user = await _users.FindByNameAsync(command.UserName)
                ?? await _users.FindByEmailAsync(command.UserName);
        if (user is null || !user.IsActive)
            return new LoginResult(false, null, "Invalid credentials.");

        var pw = await _signin.CheckPasswordSignInAsync(user, command.Password, lockoutOnFailure: true);
        if (!pw.Succeeded)
            return new LoginResult(false, null, pw.IsLockedOut
                ? "Account is locked. Try again later."
                : "Invalid credentials.");

        var pair = await _tokens.Issue(user, command.Ip, ct);
        var profile = await BuildProfile(_users, user);
        return new LoginResult(true,
            new LoginResponse(pair.AccessToken, pair.RefreshToken, pair.AccessExpiresAtUtc, profile),
            null);
    }

    internal static async Task<UserProfile> BuildProfile(UserManager<ApplicationUser> users, ApplicationUser user)
    {
        var roles = await users.GetRolesAsync(user);
        var clientIds = (user.AssignedClientIdsCsv ?? string.Empty)
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => Guid.TryParse(s, out var g) ? g : Guid.Empty)
            .Where(g => g != Guid.Empty)
            .ToList();
        return new UserProfile(
            user.Id, user.UserName ?? "", user.Email,
            user.FullName, user.SapNo, user.CertNo,
            roles.ToList(), clientIds);
    }
}

public sealed class RefreshCommandHandler : ICommandHandler<RefreshCommand, LoginResult>
{
    private readonly IJwtTokenService _tokens;
    private readonly UserManager<ApplicationUser> _users;

    public RefreshCommandHandler(IJwtTokenService tokens, UserManager<ApplicationUser> users)
    {
        _tokens = tokens;
        _users = users;
    }

    public async Task<LoginResult> Handle(RefreshCommand command, CancellationToken ct)
    {
        var pair = await _tokens.Refresh(command.RefreshToken, command.Ip, ct);
        if (pair is null) return new LoginResult(false, null, "Invalid or expired refresh token.");

        // The token service rotates and persists; we need the user for the profile.
        // Decode user id from the new access token's `sub` claim cheaply by re-querying via the
        // refresh row's UserId — but we don't have that here. Simplest: trust the access token
        // and resolve the user from its sub.
        var jwt = new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler().ReadJwtToken(pair.AccessToken);
        var sub = jwt.Subject;
        var user = await _users.FindByIdAsync(sub);
        if (user is null) return new LoginResult(false, null, "User not found.");
        var profile = await LoginCommandHandler.BuildProfile(_users, user);

        return new LoginResult(true,
            new LoginResponse(pair.AccessToken, pair.RefreshToken, pair.AccessExpiresAtUtc, profile),
            null);
    }
}

public sealed class GetCurrentUserHandler : IQueryHandler<GetCurrentUserQuery, UserProfile?>
{
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantContext _tenant;

    public GetCurrentUserHandler(UserManager<ApplicationUser> users, ITenantContext tenant)
    {
        _users = users;
        _tenant = tenant;
    }

    public async Task<UserProfile?> Handle(GetCurrentUserQuery query, CancellationToken ct)
    {
        if (_tenant.IsAnonymous || _tenant.UserId is null) return null;
        var user = await _users.FindByIdAsync(_tenant.UserId);
        return user is null ? null : await LoginCommandHandler.BuildProfile(_users, user);
    }
}
