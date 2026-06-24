using TuvInspection.Infrastructure.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class SaicChecklistCatalogTests
{
    [Fact]
    public void Loads_pilot_7007_with_items()
    {
        var doc = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(doc);
        Assert.Equal("SAIC-U-7007", doc!.SaicNumber);
        Assert.True(doc.Items.Count >= 90, $"expected ~92+ items, got {doc.Items.Count}");
        Assert.All(doc.Items, i => Assert.False(string.IsNullOrWhiteSpace(i.AcceptanceCriteria)));
    }

    [Fact]
    public void Unknown_number_returns_null()
        => Assert.Null(new SaicChecklistCatalog().Get("SAIC-U-9999"));
}
