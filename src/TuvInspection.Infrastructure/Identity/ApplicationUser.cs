using Microsoft.AspNetCore.Identity;

namespace TuvInspection.Infrastructure.Identity;

public class ApplicationUser : IdentityUser
{
    public string? FullName { get; set; }
    public string? SapNo { get; set; }
    public string? CertNo { get; set; }

    /// <summary>Comma-separated list of Client Guids the user is assigned to. Empty = none.</summary>
    public string AssignedClientIdsCsv { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

public class ApplicationRole : IdentityRole
{
    public string? Description { get; set; }
}

/// <summary>
/// Refresh token row — separate from Identity tables so we can rotate freely without
/// touching Microsoft.AspNetCore.Identity internals.
/// </summary>
public class RefreshToken
{
    public Guid Id { get; set; }
    public string UserId { get; set; } = default!;
    public string TokenHash { get; set; } = default!;     // store hash only; raw token issued once
    public DateTime IssuedAtUtc { get; set; }
    public DateTime ExpiresAtUtc { get; set; }
    public DateTime? RevokedAtUtc { get; set; }
    public string? ReplacedByTokenHash { get; set; }
    public string? CreatedFromIp { get; set; }
}
