using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Clients;

/// <summary>
/// Imports clients from an Excel sheet. Manager-only.
/// Header columns expected (case-insensitive):
///   Name · Code · Address · ContactName · ContactPhone · ContactEmail
/// Existing client codes are skipped (idempotent).
/// </summary>
public sealed class ClientImportService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public ClientImportService(AppDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db; _tenant = tenant; _clock = clock;
        ExcelPackage.License.SetNonCommercialPersonal("TuvInspection-Dev");
    }

    public async Task<EquipmentImportResult> Import(Stream excelStream, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager))
            throw new UnauthorizedAccessException("Only Manager may import clients.");

        using var pkg = new ExcelPackage(excelStream);
        var ws = pkg.Workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("Workbook has no worksheets.");

        var headers = ImportHelpers.ReadHeaders(ws);
        var existingCodes = new HashSet<string>(
            await _db.Clients.IgnoreQueryFilters().Select(c => c.Code).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var (imported, skipped, errors) = (0, 0, new List<string>());

        for (int row = 2; row <= (ws.Dimension?.End.Row ?? 1); row++)
        {
            try
            {
                var name = ImportHelpers.Cell(ws, row, headers, "Name");
                var code = ImportHelpers.Cell(ws, row, headers, "Code")?.ToUpperInvariant();
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(code)) continue;
                if (existingCodes.Contains(code)) { skipped++; continue; }

                var client = new Client(Guid.NewGuid(), name, code);
                client.UpdateAddress(ImportHelpers.Cell(ws, row, headers, "Address"));
                client.UpdateContact(
                    ImportHelpers.Cell(ws, row, headers, "ContactName"),
                    ImportHelpers.Cell(ws, row, headers, "ContactPhone"),
                    ImportHelpers.Cell(ws, row, headers, "ContactEmail"));
                client.CreatedAtUtc = _clock.UtcNow;
                client.CreatedById = _tenant.UserId;

                _db.Clients.Add(client);
                existingCodes.Add(code);
                imported++;
            }
            catch (Exception ex)
            {
                errors.Add($"Row {row}: {ex.Message}");
            }
        }

        if (imported > 0) await _db.SaveChangesAsync(ct);
        return new EquipmentImportResult(imported, skipped, errors);
    }
}
