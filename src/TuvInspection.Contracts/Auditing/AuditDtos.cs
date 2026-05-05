namespace TuvInspection.Contracts.Auditing;

public sealed record AuditLogRowDto(
    Guid Id,
    string EntityName,
    string EntityId,
    string Action,
    string? ActorUserId,
    string? ActorUserName,
    string? ActorRole,
    DateTime AtUtc,
    string? Ip,
    string? BeforeJson,
    string? AfterJson,
    string PreviousHash,
    string CurrentHash);
