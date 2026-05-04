using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.Stickers;

namespace TuvInspection.Application.Stickers;

public sealed record ListStickersQuery(
    StickerStateDto? State,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<StickerListItemDto>>;

public sealed record GetStickerStockSummaryQuery() : IQuery<StickerStockSummaryDto>;

public sealed record GetStickerPublicViewQuery(string StickerNo) : IQuery<StickerPublicViewDto?>;

public sealed record ProcureStickerStockCommand(int Count) : ICommand<int>;

public sealed record VoidStickerCommand(Guid Id, string Reason) : ICommand<StickerListItemDto>;
