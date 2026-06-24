using TuvInspection.Domain.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class SaicChecklistMapTests
{
    [Theory]
    [InlineData("CR01", "Crawler Crane", "SAIC-U-7007")]
    [InlineData("CR04", "Floating Crane", "SAIC-U-7009")]
    [InlineData("CR04", "Tower Crane", "SAIC-U-7003")]
    [InlineData("CR11", "Jib Crane", "SAIC-U-7011")]
    public void Resolves_known_equipment_types(string cat, string type, string expected)
        => Assert.Equal(expected, SaicChecklistMap.Resolve(cat, type));

    [Fact]
    public void Unknown_type_returns_null()
        => Assert.Null(SaicChecklistMap.Resolve("CR01", "Nonexistent Crane"));
}
