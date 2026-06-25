using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Stickers;

namespace TuvInspection.Application.Stickers;

public sealed record ListStickersQuery(
    StickerStateDto? State,
    StickerColorDto? Color,
    string? AssignedToInspectorId,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<StickerListItemDto>>;

public sealed record GetStickerStockSummaryQuery() : IQuery<StickerStockSummaryDto>;

public sealed record GetStickerPublicViewQuery(string StickerNo) : IQuery<StickerPublicViewDto?>;

public sealed record ProcureStickerStockCommand(int Count, StickerColorDto Color) : ICommand<int>;

public sealed record VoidStickerCommand(Guid Id, string Reason) : ICommand<StickerListItemDto>;

public sealed record AssignStickersToInspectorCommand(
    string InspectorUserId,
    StickerColorDto Color,
    int Count,
    Guid? FromRequestId) : ICommand<int>;

