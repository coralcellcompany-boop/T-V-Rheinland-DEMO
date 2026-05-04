using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Equipment;

namespace TuvInspection.Application.Equipment;

public sealed record ListDefectCodesQuery(Guid? EquipmentTypeId, bool IncludeInactive)
    : IQuery<IReadOnlyList<DefectCodeDto>>;

public sealed record CreateDefectCodeCommand(CreateDefectCodeRequest Body)
    : ICommand<DefectCodeDto>;

public sealed record UpdateDefectCodeCommand(Guid Id, UpdateDefectCodeRequest Body)
    : ICommand<DefectCodeDto>;
