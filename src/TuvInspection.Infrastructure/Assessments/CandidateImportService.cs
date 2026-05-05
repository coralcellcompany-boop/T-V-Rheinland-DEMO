using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Contracts.Equipment;
using TuvInspection.Domain.Assessments;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Assessments;

/// <summary>
/// Imports candidates from an Excel sheet for a single client. Header columns expected
/// (case-insensitive): FullName · IdNumber · Phone · Email · EmployeeNo · Nationality ·
/// DateOfBirth (yyyy-MM-dd). Existing IdNumber rows are skipped (idempotent).
/// </summary>
public sealed class CandidateImportService
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public CandidateImportService(AppDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db; _tenant = tenant; _clock = clock;
        ExcelPackage.License.SetNonCommercialPersonal("TuvInspection-Dev");
    }

    public async Task<EquipmentImportResult> Import(Guid clientId, Stream excelStream, CancellationToken ct)
    {
        if (!_tenant.IsInRole(Roles.Manager) && !_tenant.AssignedClientIds.Contains(clientId))
            throw new UnauthorizedAccessException("You can only import candidates for your assigned clients.");

        var clientExists = await _db.Clients.IgnoreQueryFilters().AnyAsync(c => c.Id == clientId, ct);
        if (!clientExists) throw new ArgumentException($"Unknown client {clientId}.");

        using var pkg = new ExcelPackage(excelStream);
        var ws = pkg.Workbook.Worksheets.FirstOrDefault()
            ?? throw new ArgumentException("Workbook has no worksheets.");

        var headers = ImportHelpers.ReadHeaders(ws);
        var existingIds = new HashSet<string>(
            await _db.Candidates.IgnoreQueryFilters()
                .Where(c => c.ClientId == clientId)
                .Select(c => c.IdentificationNumber).ToListAsync(ct),
            StringComparer.OrdinalIgnoreCase);

        var (imported, skipped, errors) = (0, 0, new List<string>());

        for (int row = 2; row <= (ws.Dimension?.End.Row ?? 1); row++)
        {
            try
            {
                var name = ImportHelpers.Cell(ws, row, headers, "FullName");
                var idNo = ImportHelpers.Cell(ws, row, headers, "IdNumber");
                if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(idNo)) continue;
                if (existingIds.Contains(idNo)) { skipped++; continue; }

                var dobRaw = ImportHelpers.Cell(ws, row, headers, "DateOfBirth");
                DateOnly? dob = DateOnly.TryParse(dobRaw, out var d) ? d : null;

                var c = new Candidate(Guid.NewGuid(), clientId, name, idNo);
                c.UpdateProfile(name, idNo,
                    ImportHelpers.Cell(ws, row, headers, "Phone"),
                    ImportHelpers.Cell(ws, row, headers, "Email"),
                    ImportHelpers.Cell(ws, row, headers, "EmployeeNo"),
                    ImportHelpers.Cell(ws, row, headers, "Nationality"),
                    dob);
                c.CreatedAtUtc = _clock.UtcNow;
                c.CreatedById = _tenant.UserId;

                _db.Candidates.Add(c);
                existingIds.Add(idNo);
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
