namespace TuvInspection.Contracts.BlueSticker;

/// <summary>A Saudi Aramco inspection checklist (SAIC-U-70##) resolved for an equipment type.
/// Items are flattened across the PDF's sections; section headers are carried so the UI can group.</summary>
public sealed record SaicChecklistDto(
    string SaicNumber,
    string Title,
    IReadOnlyList<SaicChecklistItemDto> Items);

public sealed record SaicChecklistItemDto(
    string ItemNo,
    string AcceptanceCriteria,
    string ReferenceStandard,
    string? SectionNo,
    string? SectionTitle);
