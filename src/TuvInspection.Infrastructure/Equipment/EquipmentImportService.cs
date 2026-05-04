using OfficeOpenXml;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;
using AramcoCategory = TuvInspection.Domain.Equipment.AramcoCategory;

namespace TuvInspection.Infrastructure.Equipment;

/// <summary>
/// Imports equipment rows from an Excel sheet. Header columns expected (case-insensitive):
///   IdNo · SerialNo · EquipmentType · AramcoCategory · Manufacturer · Model · Year · SWL · Location
/// EquipmentType matches by Name; AramcoCategory accepts "CR01" through "CR14" or empty.
/// </summary>
public sealed class EquipmentImportService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public EquipmentImportService(AppDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
        ExcelPackage.License.SetNonCommercialPersonal("TuvInspection-Dev");
    }

    public async Task<EquipmentImportResult> Import(Guid clientId, Stream excelStream, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(clientId))
            throw new UnauthorizedAccessException("You can only import equipment for your assigned clients.");

        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == clientId, ct);
        if (!clientExists) throw new ArgumentException($"Unknown client {clientId}.");

        var typeMap = await _db.EquipmentTypes
            .Where(t => t.IsActive)
            .ToDictionaryAsync(t => t.Name, t => t.Id, StringComparer.OrdinalIgnoreCase, ct);

        using var pkg = new ExcelPackage(excelStream);
        var ws = pkg.Workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("Workbook has no worksheets.");

        var headers = ReadHeaders(ws);
        var (importedCount, skipped, errors) = (0, 0, new List<string>());
        int existingCount = await _db.Equipment.IgnoreQueryFilters()
            .Where(e => e.ClientId == clientId).CountAsync(ct);

        var existingIdNos = new HashSet<string>(await _db.Equipment.IgnoreQueryFilters()
            .Where(e => e.ClientId == clientId)
            .Select(e => e.IdNo).ToListAsync(ct), StringComparer.OrdinalIgnoreCase);

        for (int row = 2; row <= ws.Dimension.End.Row; row++)
        {
            try
            {
                var idNo = (Cell(ws, row, headers, "IdNo") ?? "").Trim();
                if (string.IsNullOrWhiteSpace(idNo)) continue;
                if (existingIdNos.Contains(idNo)) { skipped++; continue; }

                var typeName = (Cell(ws, row, headers, "EquipmentType") ?? "").Trim();
                if (!typeMap.TryGetValue(typeName, out var typeId))
                {
                    errors.Add($"Row {row}: unknown equipment type '{typeName}'.");
                    continue;
                }

                var category = ParseCategory(Cell(ws, row, headers, "AramcoCategory"));

                var equipment = new EquipmentEntity(Guid.NewGuid(), clientId, typeId, idNo, category);
                equipment.UpdateIdentification(idNo, Cell(ws, row, headers, "SerialNo"));
                equipment.UpdateSpec(
                    Cell(ws, row, headers, "Manufacturer"),
                    Cell(ws, row, headers, "Model"),
                    int.TryParse(Cell(ws, row, headers, "Year"), out var y) ? y : null,
                    Cell(ws, row, headers, "SWL"));
                equipment.UpdateLocation(Cell(ws, row, headers, "Location"));
                equipment.CreatedAtUtc = _clock.UtcNow;
                equipment.CreatedById = _tenant.UserId;

                _db.Equipment.Add(equipment);
                existingIdNos.Add(idNo);
                importedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {row}: {ex.Message}");
            }
        }

        if (importedCount > 0) await _db.SaveChangesAsync(ct);
        return new EquipmentImportResult(importedCount, skipped, errors);
    }

    private static Dictionary<string, int> ReadHeaders(ExcelWorksheet ws)
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

    private static string? Cell(ExcelWorksheet ws, int row, Dictionary<string, int> headers, string name)
    {
        if (!headers.TryGetValue(name, out var col)) return null;
        var v = ws.Cells[row, col].Text;
        return string.IsNullOrWhiteSpace(v) ? null : v.Trim();
    }

    private static AramcoCategory? ParseCategory(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return null;
        var s = input.Trim().ToUpperInvariant();
        // Accept "CR01", "1", "CR01_MobileCrane", etc. — use the digits.
        if (s.StartsWith("CR")) s = s[2..];
        var digits = new string(s.TakeWhile(char.IsDigit).ToArray());
        if (int.TryParse(digits, out var n) && n >= 1 && n <= 14) return (AramcoCategory)n;
        return null;
    }
}
