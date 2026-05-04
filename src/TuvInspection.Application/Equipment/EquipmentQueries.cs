using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Equipment;

namespace TuvInspection.Application.Equipment;

public sealed record ListEquipmentTypesQuery() : IQuery<IReadOnlyList<EquipmentTypeDto>>;

public sealed record ListEquipmentQuery(
    Guid? ClientId,
    Guid? EquipmentTypeId,
    AramcoCategoryDto? AramcoCategory,
    EquipmentStatusDto? Status,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<EquipmentListItemDto>>;

public sealed record GetEquipmentByIdQuery(Guid Id) : IQuery<EquipmentDetailDto?>;

public sealed record CreateEquipmentCommand(CreateEquipmentRequest Body) : ICommand<EquipmentDetailDto>;

public sealed record UpdateEquipmentCommand(Guid Id, UpdateEquipmentRequest Body) : ICommand<EquipmentDetailDto>;
