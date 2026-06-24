using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>Loads the embedded SAIC checklist JSON resources and flattens their sections into the
/// resolve DTO. Results are cached per process — the catalog is immutable reference data.</summary>
public sealed class SaicChecklistCatalog
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private static readonly ConcurrentDictionary<string, SaicChecklistDto?> Cache = new();
    private static readonly Assembly Asm = typeof(SaicChecklistCatalog).Assembly;

    private sealed record RawDoc(string SaicNumber, string Title, List<RawSection> Sections);
    private sealed record RawSection(string No, string Title, List<RawItem> Items);
    private sealed record RawItem(string ItemNo, string AcceptanceCriteria, string ReferenceStandard);

    public SaicChecklistDto? Get(string saicNumber) => Cache.GetOrAdd(saicNumber, Load);

    private static SaicChecklistDto? Load(string saicNumber)
    {
        var name = Asm.GetManifestResourceNames()
            .FirstOrDefault(n => n.EndsWith($"{saicNumber}.json", StringComparison.OrdinalIgnoreCase)
                && n.Contains("SaicChecklists", StringComparison.OrdinalIgnoreCase));
        // Not-found is a normal "unmapped / not embedded yet" outcome → null (controller maps to 204).
        // A resource that IS present but won't parse is a build/extraction defect, not a normal miss,
        // so let JsonSerializer throw rather than masking a corrupt catalog entry as an empty 204.
        if (name is null) return null;
        using var stream = Asm.GetManifestResourceStream(name)!; // non-null: name came from GetManifestResourceNames()
        var raw = JsonSerializer.Deserialize<RawDoc>(stream, Json)
            ?? throw new InvalidOperationException($"Embedded SAIC checklist '{name}' deserialized to null.");
        var items = raw.Sections
            .SelectMany(s => s.Items.Select(i =>
                new SaicChecklistItemDto(i.ItemNo, i.AcceptanceCriteria, i.ReferenceStandard, s.No, s.Title)))
            .ToList();
        return new SaicChecklistDto(raw.SaicNumber, raw.Title, items);
    }
}
