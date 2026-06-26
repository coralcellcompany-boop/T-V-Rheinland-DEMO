using System.Text;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportPdfRendererTests
{
    [Fact]
    public void Fallback_renders_valid_pdf_including_checklist_pages()
    {
        var checklist = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(checklist);

        var bytes = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(), checklist);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
        Assert.True(bytes.Length > 3000, $"expected a multi-page PDF, got {bytes.Length} bytes");
    }

    [Fact]
    public void Fallback_without_checklist_still_renders_valid_pdf()
    {
        var bytes = BlueStickerReportPdfRenderer.RenderFallback(
            BlueStickerTestData.SampleReport(checklistNumber: null), checklist: null);

        Assert.Equal("%PDF", Encoding.ASCII.GetString(bytes, 0, 4));
    }
}
