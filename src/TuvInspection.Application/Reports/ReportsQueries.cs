using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Reports;

namespace TuvInspection.Application.Reports;

public sealed record GetMonthlyStatsQuery(int Months) : IQuery<IReadOnlyList<MonthlyStatsRowDto>>;
public sealed record GetInspectorProductivityQuery(int Days) : IQuery<IReadOnlyList<InspectorProductivityRowDto>>;
public sealed record GetDueSoonQuery(int Days) : IQuery<IReadOnlyList<DueSoonRowDto>>;
public sealed record GetOverdueQuery() : IQuery<IReadOnlyList<OverdueRowDto>>;
