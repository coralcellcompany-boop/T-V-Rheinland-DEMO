namespace TuvInspection.Domain.Common;

public interface IAuditable
{
    DateTime CreatedAtUtc { get; set; }
    string? CreatedById { get; set; }
    DateTime? UpdatedAtUtc { get; set; }
    string? UpdatedById { get; set; }
}

/// <summary>
/// Marker interface for entities that are scoped to a single Client (tenant).
/// The EF global query filter scopes queries by <see cref="ClientId"/> against the current
/// <c>ITenantContext.AssignedClientIds</c>.
/// </summary>
public interface ITenantScoped
{
    Guid ClientId { get; }
}
