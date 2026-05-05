using OfficeOpenXml;

namespace TuvInspection.Infrastructure;

internal static class ImportHelpers
{
    public static Dictionary<string, int> ReadHeaders(ExcelWorksheet ws)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        if (ws.Dimension is null) return map;
        for (int col = 1; col <= ws.Dimension.End.Column; col++)
        {
            var name = ws.Cells[1, col].Text?.Trim();
            if (!string.IsNullOrEmpty(name)) map[name] = col;
        }
        return map;
    }

    public static string? Cell(ExcelWorksheet ws, int row, Dictionary<string, int> headers, string name)
    {
        if (!headers.TryGetValue(name, out var col)) return null;
        var v = ws.Cells[row, col].Text;
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }
}
