using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Application.BlueSticker;

/// <summary>Resolve the SAIC checklist (number + items) for an Aramco category + equipment type.
/// Returns null when the type is unmapped or its catalog entry isn't available yet.</summary>
public sealed record ResolveSaicChecklistQuery(string Category, string EquipmentType)
    : IQuery<SaicChecklistDto?>;
