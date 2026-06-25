using TuvInspection.Domain.BlueSticker;
using TuvInspection.Infrastructure.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class SaicChecklistCoverageTests
{
    [Fact]
    public void Every_mapped_saic_number_has_a_catalog_entry()
    {
        var catalog = new SaicChecklistCatalog();
        foreach (var saic in SaicChecklistMap.AllSaicNumbers())
        {
            var doc = catalog.Get(saic);
            Assert.True(doc is { Items.Count: > 0 }, $"missing/empty catalog for {saic}");
        }
    }
}
