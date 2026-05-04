namespace TuvInspection.Application.Common;

/// <summary>
/// Per-request tenant + identity context. Bound from the incoming JWT claims by the API host.
/// Consumed by the EF global query filter to scope tenant-scoped entities by ClientId, and by
/// the audit interceptor to stamp actor identity.
/// </summary>
public interface ITenantContext
{
    /// <summary>True when there is no authenticated user (background jobs, anonymous endpoints).</summary>
    bool IsAnonymous { get; }

    string? UserId { get; }
    string? UserName { get; }

    /// <summary>Caller's primary role (for audit) — first of the role claims if multiple.</summary>
    string? PrimaryRole { get; }

    IReadOnlySet<string> Roles { get; }

    /// <summary>Clients this user is allowed to operate on. Empty = none. Manager bypasses scoping.</summary>
    IReadOnlySet<Guid> AssignedClientIds { get; }

    /// <summary>If the user has switched to a single client view in the UI, this holds it.</summary>
    Guid? ActiveClientId { get; }

    string? IpAddress { get; }

    bool IsInRole(string role);
}
