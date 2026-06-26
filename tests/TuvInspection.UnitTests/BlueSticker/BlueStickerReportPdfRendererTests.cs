using System.Text;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportPdfRendererTests
{
    [Fact]
    public void Fallback_with_checklist_adds_pages_beyond_the_plain_report()
    {
        var checklist = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(checklist);

        var withChecklist = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(), checklist);
        var plain = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(checklistNumber: null), checklist: null);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(withChecklist, 0, 4));
        // A 90+ item checklist page adds substantial content — far more than any single-page variation.
        Assert.True(withChecklist.Length > plain.Length + 1000,
            $"expected checklist render ({withChecklist.Length} bytes) to dwarf the plain report ({plain.Length} bytes)");
    }

    [Fact]
    public void Fallback_without_checklist_still_renders_valid_pdf()
    {
        var bytes = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(checklistNumber: null), checklist: null);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
