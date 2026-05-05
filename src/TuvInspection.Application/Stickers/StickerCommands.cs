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

// ─── Sticker requests ───
public sealed record ListStickerRequestsQuery(
    StickerRequestStateDto? State,
    string? InspectorUserId,
    int Page,
    int PageSize) : IQuery<PagedResult<StickerRequestDto>>;

public sealed record CreateStickerRequestCommand(CreateStickerRequest Body) : ICommand<StickerRequestDto>;

public sealed record ApproveStickerRequestCommand(Guid Id, string? Comments) : ICommand<StickerRequestDto>;

public sealed record RejectStickerRequestCommand(Guid Id, string Reason) : ICommand<StickerRequestDto>;

public sealed record CancelStickerRequestCommand(Guid Id) : ICommand<StickerRequestDto>;
