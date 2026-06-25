using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Domain.BlueSticker;

namespace TuvInspection.Infrastructure.BlueSticker;

public sealed class ResolveSaicChecklistHandler
    : IQueryHandler<ResolveSaicChecklistQuery, SaicChecklistDto?>
{
    private readonly SaicChecklistCatalog _catalog = new();

    public Task<SaicChecklistDto?> Handle(ResolveSaicChecklistQuery q, CancellationToken ct)
    {
        var saic = SaicChecklistMap.Resolve(q.Category, q.EquipmentType);
        return Task.FromResult(saic is null ? null : _catalog.Get(saic));
    }
}
