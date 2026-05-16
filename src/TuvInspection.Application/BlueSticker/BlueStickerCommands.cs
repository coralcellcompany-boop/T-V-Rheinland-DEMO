using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Application.BlueSticker;

public sealed record CreateBlueStickerReportsCommand(CreateBlueStickerReportsRequest Body)
    : ICommand<IReadOnlyList<BlueStickerReportDetailDto>>;

public sealed record UpdateBlueStickerInspectionCommand(Guid Id, UpdateBlueStickerInspectionRequest Body)
    : ICommand<BlueStickerReportDetailDto>;

public sealed record FireBlueStickerTriggerCommand(
    Guid Id, BlueStickerTriggerDto Trigger, BlueStickerTransitionRequest? Body)
    : ICommand<BlueStickerReportDetailDto>;

public sealed record RequestClientOtpCommand(Guid Id) : ICommand<BlueStickerReportDetailDto>;

public sealed record VerifyOtpAndSignCommand(Guid Id, VerifyOtpAndSignRequest Body)
    : ICommand<BlueStickerReportDetailDto>;

public sealed record GetBlueStickerReportByIdQuery(Guid Id) : IQuery<BlueStickerReportDetailDto?>;

public sealed record ListBlueStickerReportsQuery(
    Guid? JobOrderId, BlueStickerReportStateDto? State, string? Search, int Page, int PageSize)
    : IQuery<PagedResult<BlueStickerReportListItemDto>>;
