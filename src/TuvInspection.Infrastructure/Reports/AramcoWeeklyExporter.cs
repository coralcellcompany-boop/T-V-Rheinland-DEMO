using Microsoft.EntityFrameworkCore;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using System.Drawing;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Reports;

/// <summary>
/// Generates the Aramco Contractor Cranes Tracking weekly xlsx per SRS §5.5.7.
/// Filters to Blue Sticker certificates (i.e. Aramco-categorized equipment) approved
/// in the Mon→Sun window straddling the cutoff date.
/// </summary>
public sealed class AramcoWeeklyExporter
{
    private readonly AppDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    public AramcoWeeklyExporter(AppDbContext db, ITenantContext tenant, IClock clock)
    {
        _db = db;
        _tenant = tenant;
        _clock = clock;
        ExcelPackage.License.SetNonCommercialPersonal("TuvInspection-Dev");
    }

    public async Task<(byte[] bytes, string fileName)> Generate(DateOnly cutoff, Guid? clientId, CancellationToken ct)
    {
        // Find the Mon..Sun window that contains the cutoff.
        var dow = (int)cutoff.DayOfWeek;            // Sunday=0, Monday=1, ..., Saturday=6
        var daysFromMonday = dow == 0 ? 6 : dow - 1;
        var weekStart = cutoff.AddDays(-daysFromMonday);
        var weekEnd = weekStart.AddDays(6);

        IQueryable<InspectionCertificate> certs = _tenant.IsInRole(Roles.Manager)
            ? _db.Certificates.IgnoreQueryFilters().AsNoTracking()
            : _db.Certificates.AsNoTracking();
        if (clientId is { } cid) certs = certs.Where(c => c.ClientId == cid);
        // Approved (or downstream) within the week, with a sticker number (= Blue Sticker).
        var startUtc = weekStart.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var endUtc = weekEnd.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var rows = await certs
            .Where(c => c.StickerNo != null
                && c.CreatedAtUtc >= startUtc && c.CreatedAtUtc < endUtc
                && (c.State == CertificateState.Approved
                 || c.State == CertificateState.ClientSent
                 || c.State == CertificateState.ClientAccepted
                 || c.State == CertificateState.Archived))
            .OrderBy(c => c.CreatedAtUtc)
            .Join(_db.Clients.IgnoreQueryFilters(), c => c.ClientId, cl => cl.Id, (c, cl) => new { c, cl })
            .Join(_db.Equipment.IgnoreQueryFilters(), x => x.c.EquipmentId, e => e.Id, (x, e) => new { x.c, x.cl, e })
            .Join(_db.EquipmentTypes, x => x.e.EquipmentTypeId, t => t.Id, (x, t) =>
                new
                {
                    x.c.CertificateNo, x.c.InspectionDate, x.c.NextDueDate,
                    x.c.InspectionType, x.c.LoadTest, x.c.Result, x.c.StickerNo,
                    x.cl.Name, ClientCode = x.cl.Code,
                    EquipIdNo = x.e.IdNo, EquipSerial = x.e.SerialNo,
                    EquipLocation = x.e.Location,
                    TypeName = t.Name,
                    AramcoCategory = x.e.AramcoCategory,
                })
            .ToListAsync(ct);

        using var pkg = new ExcelPackage();
        var ws = pkg.Workbook.Worksheets.Add("Contractor Cranes Tracking");

        // Cover row
        ws.Cells["A1"].Value = "Aramco Contractor Cranes Tracking — Weekly Submission";
        ws.Cells["A1:R1"].Merge = true;
        var titleStyle = ws.Cells["A1"].Style;
        titleStyle.Font.Size = 14;
        titleStyle.Font.Bold = true;
        titleStyle.Font.Color.SetColor(Color.White);
        titleStyle.Fill.PatternType = ExcelFillStyle.Solid;
        titleStyle.Fill.BackgroundColor.SetColor(Color.FromArgb(10, 61, 98));
        titleStyle.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        ws.Row(1).Height = 24;

        ws.Cells["A2"].Value = $"Period: {weekStart:dd MMM yyyy} — {weekEnd:dd MMM yyyy}     " +
                               $"Issuer: TÜV Rheinland Arabia LLC     " +
                               $"Generated: {_clock.UtcNow:yyyy-MM-dd HH:mm} UTC";
        ws.Cells["A2:R2"].Merge = true;
        ws.Cells["A2"].Style.Font.Italic = true;
        ws.Cells["A2"].Style.Font.Color.SetColor(Color.FromArgb(71, 85, 105));

        // Header row
        var headers = new[]
        {
            "S.No", "P.O", "Equipment Owner",
            "Equipment Type", "Equipment ID No", "Equipment Serial No",
            "Equipment Location", "Inspection Type", "Load Test",
            "Inspection Performed Date", "Next Inspection Due Date",
            "Inspection Result", "Inspector Assigned", "Inspector Cert No",
            "Sticker Number", "Report Number", "QR-CODE",
            "Aramco Category",
        };
        for (int i = 0; i < headers.Length; i++)
        {
            var cell = ws.Cells[4, i + 1];
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.PatternType = ExcelFillStyle.Solid;
            cell.Style.Fill.BackgroundColor.SetColor(Color.FromArgb(241, 245, 249));
            cell.Style.Border.Bottom.Style = ExcelBorderStyle.Thin;
            cell.Style.HorizontalAlignment = ExcelHorizontalAlignment.Center;
        }
        ws.Row(4).Height = 28;

        // Data rows
        for (int r = 0; r < rows.Count; r++)
        {
            var row = rows[r];
            int rr = r + 5;
            ws.Cells[rr, 1].Value = r + 1;
            ws.Cells[rr, 2].Value = "";                                 // PO not modeled yet
            ws.Cells[rr, 3].Value = $"{row.Name} ({row.ClientCode})";
            ws.Cells[rr, 4].Value = row.TypeName;
            ws.Cells[rr, 5].Value = row.EquipIdNo;
            ws.Cells[rr, 6].Value = row.EquipSerial ?? "";
            ws.Cells[rr, 7].Value = row.EquipLocation ?? "";
            ws.Cells[rr, 8].Value = row.InspectionType switch
            {
                CertificateInspectionType.PeriodicInspection => "P.I.",
                CertificateInspectionType.ReInspection => "Re.I.",
                CertificateInspectionType.InitialInspection => "I.I.",
                _ => row.InspectionType.ToString(),
            };
            ws.Cells[rr, 9].Value = row.LoadTest switch
            {
                LoadTestKind.None => "",
                LoadTestKind.Mechanical => "M",
                LoadTestKind.Witnessed => "W",
                LoadTestKind.Performed => "P",
                _ => row.LoadTest.ToString(),
            };
            ws.Cells[rr, 10].Value = row.InspectionDate.ToString("yyyy-MM-dd");
            ws.Cells[rr, 11].Value = row.NextDueDate?.ToString("yyyy-MM-dd") ?? "";
            ws.Cells[rr, 12].Value = row.Result switch
            {
                InspectionResult.Pass => "P",
                InspectionResult.Fail => "F",
                InspectionResult.FailWithObservations => "F-Obs",
                _ => "",
            };
            ws.Cells[rr, 13].Value = "";                                // Inspector name placeholder
            ws.Cells[rr, 14].Value = "";                                // Cert No placeholder
            ws.Cells[rr, 15].Value = row.StickerNo;
            ws.Cells[rr, 16].Value = row.CertificateNo;
            ws.Cells[rr, 17].Value = $"http://localhost:4201/verify/{row.StickerNo}";
            ws.Cells[rr, 18].Value = row.AramcoCategory?.ToString() ?? "";
        }

        // Auto-size and final touches
        for (int col = 1; col <= headers.Length; col++)
            ws.Column(col).AutoFit();
        ws.View.FreezePanes(5, 1);

        var bytes = pkg.GetAsByteArray();
        var fileName = $"Aramco-Contractor-Tracking-{weekStart:yyyy-MM-dd}-to-{weekEnd:yyyy-MM-dd}.xlsx";
        return (bytes, fileName);
    }
}
