using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Auditing;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Application.Auditing;

/// <summary>
/// Browse the audit log. Filters cover entity type, entity id, actor, time window, and
/// free-text. Manager-only — the audit log is the most sensitive surface in the system.
/// </summary>
public sealed record ListAuditQuery(
    string? EntityName,
    string? EntityId,
    string? ActorUserId,
    DateTime? FromUtc,
    DateTime? ToUtc,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<AuditLogRowDto>>;
