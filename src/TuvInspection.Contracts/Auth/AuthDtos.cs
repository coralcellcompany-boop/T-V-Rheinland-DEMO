namespace TuvInspection.Contracts.Auth;

public sealed record LoginRequest(string UserName, string Password);

public sealed record LoginResponse(
    string AccessToken,
    string RefreshToken,
    DateTime AccessTokenExpiresAtUtc,
    UserProfile User);

public sealed record RefreshRequest(string RefreshToken);

public sealed record UserProfile(
    string Id,
    string UserName,
    string? Email,
    string? FullName,
    string? SapNo,
    string? CertNo,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> AssignedClientIds);
