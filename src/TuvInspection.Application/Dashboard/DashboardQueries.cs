using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Application.Dashboard;

public sealed record GetDashboardKpisQuery(Guid? ClientId) : IQuery<DashboardKpisDto>;

public sealed record GetRecentActivityQuery(int Limit) : IQuery<IReadOnlyList<RecentActivityItemDto>>;

public sealed record RecentActivityItemDto(
    string EntityName,
    string EntityId,
    string Action,
    string? ActorUserId,
    string? ActorRole,
    DateTime AtUtc);
