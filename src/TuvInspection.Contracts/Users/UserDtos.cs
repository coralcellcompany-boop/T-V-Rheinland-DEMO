namespace TuvInspection.Contracts.Users;

public sealed record UserListItemDto(
    string Id,
    string UserName,
    string? Email,
    string? FullName,
    string? SapNo,
    string? CertNo,
    bool IsActive,
    bool IsLockedOut,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> AssignedClientIds,
    DateTime CreatedAtUtc);

public sealed record CreateUserRequest(
    string Email,
    string FullName,
    string? SapNo,
    string? CertNo,
    string Password,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid>? AssignedClientIds);

public sealed record UpdateUserRequest(
    string FullName,
    string? SapNo,
    string? CertNo,
    bool IsActive,
    IReadOnlyList<string> Roles,
    IReadOnlyList<Guid> AssignedClientIds);

public sealed record ResetPasswordRequest(string NewPassword);

public sealed record UserLicenseDto(
    string? LicenseNumber,
    string? LicenseAuthority,
    string? LicenseScope,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil,
    bool IsValidNow,
    int? DaysUntilExpiry);

public sealed record UpdateUserLicenseRequest(
    string? LicenseNumber,
    string? LicenseAuthority,
    string? LicenseScope,
    DateOnly? ValidFrom,
    DateOnly? ValidUntil);

/// <summary>Lightweight inspector lookup — used by features that need to pick an inspector
/// (sticker assignment, job-order assignment) without exposing full admin user data.</summary>
public sealed record InspectorLookupDto(string Id, string DisplayName, string? Email);
