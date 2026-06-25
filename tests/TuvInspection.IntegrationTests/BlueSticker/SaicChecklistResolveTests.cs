using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.IntegrationTests.BlueSticker;

/// <summary>
/// End-to-end HTTP integration tests for the SAIC checklist resolve endpoint
/// (GET /api/saic-checklists/resolve). Exercises the real controller + DI +
/// embedded checklist catalog + JSON serialization over HTTP, replacing a flaky
/// manual browser session with a repeatable backend test.
///
/// Reuses the shared <see cref="BlueStickerApiFixture"/> (via the "BlueStickerApi"
/// collection) so no additional throwaway database is spun up.
/// </summary>
[Collection("BlueStickerApi")]
public sealed class SaicChecklistResolveTests
{
    private readonly BlueStickerApiFixture _fx;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // The verified pilot mapping: ("CR01", "Crawler Crane") → SAIC-U-7007 with 116 items.
    private const string Category = "CR01";
    private const string EquipmentType = "Crawler Crane";
    private const string ExpectedSaicNumber = "SAIC-U-7007";
    private const int ExpectedItemCount = 116;

    public SaicChecklistResolveTests(BlueStickerApiFixture fx) => _fx = fx;

    [Fact]
    public async Task Resolve_AuthenticatedMappedSelection_Returns200WithFullChecklist()
    {
        var client = await _fx.CreateAuthenticatedClient("inspector1@tuv-arabia.local");

        var resp = await client.GetAsync(
            $"/api/saic-checklists/resolve?category={Category}&equipmentType={Uri.EscapeDataString(EquipmentType)}");

        resp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Resolve failed: {await resp.Content.ReadAsStringAsync()}");

        var dto = await resp.Content.ReadFromJsonAsync<SaicChecklistDto>(_json);
        dto.Should().NotBeNull("a mapped selection must deserialize to a SaicChecklistDto");
        dto!.SaicNumber.Should().Be(ExpectedSaicNumber);
        dto.Title.Should().NotBeNullOrWhiteSpace("checklist must carry a title");
        dto.Items.Should().HaveCount(ExpectedItemCount,
            "the verified pilot count for SAIC-U-7007 is 116 items");

        dto.Items.Should().OnlyContain(
            i => !string.IsNullOrWhiteSpace(i.AcceptanceCriteria),
            "every checklist item must have a non-empty AcceptanceCriteria");

        dto.Items.Should().Contain(
            i => !string.IsNullOrWhiteSpace(i.ReferenceStandard),
            "at least one checklist item must carry a ReferenceStandard");
    }

    [Fact]
    public async Task Resolve_UnmappedEquipmentType_Returns204NoContent()
    {
        var client = await _fx.CreateAuthenticatedClient("inspector1@tuv-arabia.local");

        var resp = await client.GetAsync(
            $"/api/saic-checklists/resolve?category={Category}&equipmentType={Uri.EscapeDataString("Nonexistent Crane")}");

        resp.StatusCode.Should().Be(HttpStatusCode.NoContent,
            "an unmapped (category, equipmentType) pair must resolve to 204 NoContent");
    }

    [Fact]
    public async Task Resolve_NoBearerToken_Returns401Unauthorized()
    {
        var client = _fx.CreateClient();

        var resp = await client.GetAsync(
            $"/api/saic-checklists/resolve?category={Category}&equipmentType={Uri.EscapeDataString(EquipmentType)}");

        resp.StatusCode.Should().Be(HttpStatusCode.Unauthorized,
            "[Authorize] must reject anonymous requests");
    }
}
