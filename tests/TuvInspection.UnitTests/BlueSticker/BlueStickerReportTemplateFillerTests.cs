using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using TuvInspection.Infrastructure.BlueSticker;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportTemplateFillerTests
{
    [Fact]
    public void Fill_with_checklist_appends_second_table_with_items_and_heading()
    {
        var checklist = new SaicChecklistCatalog().Get("SAIC-U-7007");
        Assert.NotNull(checklist);

        var bytes = new BlueStickerReportTemplateFiller()
            .Fill(BlueStickerTestData.SampleReport(), checklist);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var body = doc.MainDocumentPart!.Document.Body!;
        var tables = body.Elements<Table>().ToList();

        Assert.Equal(2, tables.Count);                                  // original Annex 1 + checklist
        Assert.Contains("SAIC-U-7007", body.InnerText);                 // heading text
        Assert.Contains(checklist!.Items[0].ItemNo, tables[1].InnerText);
        Assert.Contains(checklist.Items[0].AcceptanceCriteria, tables[1].InnerText);
        Assert.Contains(checklist.Items[^1].ItemNo, tables[1].InnerText);
    }

    [Fact]
    public void Fill_without_checklist_adds_no_extra_table()
    {
        var bytes = new BlueStickerReportTemplateFiller()
            .Fill(BlueStickerTestData.SampleReport(checklistNumber: null), checklist: null);

        using var ms = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(ms, false);
        var tables = doc.MainDocumentPart!.Document.Body!.Elements<Table>().ToList();

        Assert.Single(tables);
    }
}
