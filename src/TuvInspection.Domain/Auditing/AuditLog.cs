using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Auditing;

/// <summary>
/// Immutable, hash-chained audit row. Written exclusively by
/// <c>AuditSaveChangesInterceptor</c> via a separate DbContext bound to a SQL user
/// with INSERT-only grant. ISO 17020 compliance requirement (SRS §11.3).
/// </summary>
public sealed class AuditLog
{
    public Guid Id { get; private set; }
    public string EntityName { get; private set; } = default!;
    public string EntityId { get; private set; } = default!;
    public string Action { get; private set; } = default!;       // Create/Update/Delete/Transition
    public string? ActorUserId { get; private set; }
    public string? ActorRole { get; private set; }
    public DateTime AtUtc { get; private set; }
    public string? Ip { get; private set; }
    public string? BeforeJson { get; private set; }
    public string? AfterJson { get; private set; }
    public string PreviousHash { get; private set; } = default!;
    public string CurrentHash { get; private set; } = default!;

    private AuditLog() { }

    public AuditLog(
        Guid id,
        string entityName,
        string entityId,
        string action,
        string? actorUserId,
        string? actorRole,
        DateTime atUtc,
        string? ip,
        string? beforeJson,
        string? afterJson,
        string previousHash,
        string currentHash)
    {
        Id = id;
        EntityName = entityName;
        EntityId = entityId;
        Action = action;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        AtUtc = atUtc;
        Ip = ip;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        PreviousHash = previousHash;
        CurrentHash = currentHash;
    }
}
