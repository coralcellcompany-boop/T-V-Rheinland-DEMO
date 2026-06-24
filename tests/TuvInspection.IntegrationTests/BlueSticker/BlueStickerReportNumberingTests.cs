using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using TuvInspection.Contracts.Auth;
using TuvInspection.Contracts.BlueSticker;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Equipment;
using TuvInspection.Domain.JobOrders;
using TuvInspection.Infrastructure.Identity;
using TuvInspection.Infrastructure.Outbox;
using TuvInspection.Infrastructure.Persistence;
using TuvInspection.Infrastructure.Reports;
using TuvInspection.Infrastructure.Stickers;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;

namespace TuvInspection.IntegrationTests.BlueSticker;

// ─── Fixture ─────────────────────────────────────────────────────────────────

/// <summary>
/// Isolated WebApplicationFactory backed by a dedicated throwaway SQL Server database.
/// Mirrors the BlueStickerApiFixture / JobOrderApiFixture pattern: unique DB per run,
/// "Development" environment so IdentitySeeder + DevDataSeeder run automatically,
/// dropped in DisposeAsync.
/// </summary>
public sealed class BlueStickerNumberingFixture : IAsyncLifetime
{
    private static readonly string _devServerConn =
        "Server=localhost,1433;User Id=sa;Password=Tuv_Local_Dev_2026!;TrustServerCertificate=True;Encrypt=True";

    private readonly string _dbName = $"TuvInspectionTest_{Guid.NewGuid():N}";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        await using (var conn = new SqlConnection(_devServerConn))
        {
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"CREATE DATABASE [{_dbName}]";
            await cmd.ExecuteNonQueryAsync();
        }

        var connStr = $"{_devServerConn};Database={_dbName}";

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(b =>
            {
                b.UseEnvironment("Development");
                b.UseSetting("ConnectionStrings:Application", connStr);
                b.UseSetting("ConnectionStrings:Audit", connStr);
                b.UseSetting("Jwt:Issuer", "tuv-arabia");
                b.UseSetting("Jwt:Audience", "tuv-arabia-spa");
                b.UseSetting("Jwt:SigningKey", "REPLACE_WITH_AT_LEAST_32_CHAR_DEV_SIGNING_KEY_2026");
                b.UseSetting("Jwt:AccessTokenMinutes", "60");
                b.ConfigureServices(services =>
                {
                    var hostedToRemove = new HashSet<Type>
                    {
                        typeof(OutboxProcessor),
                        typeof(StickerExpiryService),
                        typeof(AramcoWeeklyExportScheduler),
                    };
                    var hosted = services
                        .Where(d => d.ServiceType == typeof(Microsoft.Extensions.Hosting.IHostedService)
                            && d.ImplementationType is not null
                            && hostedToRemove.Contains(d.ImplementationType))
                        .ToList();
                    foreach (var h in hosted)
                        services.Remove(h);
                });
            });

        // Warm up: runs migrations + IdentitySeeder + DevDataSeeder.
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();
        SqlConnection.ClearAllPools();

        try
        {
            await using var conn = new SqlConnection(_devServerConn);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText =
                $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine(
                $"[BlueStickerNumberingFixture] Failed to drop test DB '{_dbName}': {ex.Message}");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    public HttpClient CreateClient() => Factory.CreateClient();

    public async Task<HttpClient> CreateAuthenticatedClient(string email)
    {
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, DevDataSeeder.DevPassword), _jsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Login for {email} failed");
        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>(_jsonOpts);
        login.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    public async Task DbExec(Func<AppDbContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }
}

// ─── Tests ────────────────────────────────────────────────────────────────────

[Collection("BlueStickerNumbering")]
public sealed class BlueStickerReportNumberingTests : IClassFixture<BlueStickerNumberingFixture>
{
    private readonly BlueStickerNumberingFixture _fx;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    // Pattern for a valid BSR report number
    private static readonly Regex _reportNoRegex = new(@"^BSR-\d{4}-\d{4}$", RegexOptions.Compiled);

    public BlueStickerReportNumberingTests(BlueStickerNumberingFixture fx) => _fx = fx;

    /// <summary>
    /// Regression test for the duplicate-ReportNo bug:
    ///
    /// <c>CreateBlueStickerReportsHandler</c> loops over every Aramco-categorised equipment
    /// item for the client and calls <c>BlueStickerReportNoGenerator.Next()</c> for each one
    /// before a single <c>SaveChanges</c>. The original generator read only the DB
    /// <c>CountAsync</c>, which was the same on every iteration (nothing is saved yet), so all
    /// reports got the same number (e.g. <c>BSR-2026-0001</c>) and SQL Server rejected the
    /// batch with <c>Cannot insert duplicate key … IX_BlueStickerReports_ReportNo</c>.
    ///
    /// The fix also counts <c>Added</c> <c>BlueStickerReport</c> entities already tracked in
    /// the current <c>ChangeTracker</c> (not yet saved), so each call in the loop yields a
    /// distinct sequential number (0001, 0002, …).
    ///
    /// This test FAILS without the fix (HTTP 500 / duplicate-key) and PASSES with it.
    /// </summary>
    [Fact]
    public async Task Creating_reports_for_client_with_multiple_aramco_equipment_assigns_distinct_numbers()
    {
        // ── Seed prerequisites ────────────────────────────────────────────────
        Guid clientId = default;
        Guid jobOrderId = default;

        await _fx.DbExec(async db =>
        {
            // Pick any EquipmentType that carries a real Aramco category.
            var equipType = await db.EquipmentTypes.AsNoTracking()
                .FirstAsync(t => t.AramcoCategory != null && t.AramcoCategory != AramcoCategory.None);

            // Create an isolated test client.
            var clientCode = $"NR-{Guid.NewGuid():N}".Substring(0, 10).ToUpperInvariant();
            var client = new Client(Guid.NewGuid(), "Numbering Regression Client", clientCode)
            {
                CreatedAtUtc = DateTime.UtcNow,
            };
            client.UpdateContact("Numbering Contact", "+966 50 000 0099", "nr.contact@client.example");
            client.SetAllowedServices(ServiceType.BlueSticker);
            db.Clients.Add(client);
            clientId = client.Id;

            // Seed TWO Aramco-categorised equipment items for the same client — this is the
            // minimum required to trigger the duplicate-key bug.
            for (var i = 1; i <= 2; i++)
            {
                var eq = new EquipmentEntity(
                    Guid.NewGuid(), clientId, equipType.Id,
                    $"NR-EQ-{Guid.NewGuid():N}".Substring(0, 16),
                    equipType.AramcoCategory)
                {
                    CreatedAtUtc = DateTime.UtcNow,
                };
                eq.UpdateSpec("NrMfr", "NrModel", 2023, "10 t");
                db.Equipment.Add(eq);
            }

            // Blue Sticker job order for the client.
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var jo = new JobOrder(
                Guid.NewGuid(), $"NR-JOD-{Guid.NewGuid():N}".Substring(0, 14),
                clientId, ServiceType.BlueSticker, today, today.AddDays(1))
            {
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.JobOrders.Add(jo);
            jobOrderId = jo.Id;

            await db.SaveChangesAsync();

            // Grant the coordinator (and inspector) visibility of this test client via
            // AssignedClientIdsCsv — mirrors the pattern in BlueStickerWorkflowTests.
            var staffEmails = new[]
            {
                "coordinator@tuv-arabia.local",
                "inspector1@tuv-arabia.local",
            };
            foreach (var email in staffEmails)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user is null) continue;
                var existing = string.IsNullOrEmpty(user.AssignedClientIdsCsv)
                    ? new List<string>()
                    : user.AssignedClientIdsCsv
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .ToList();
                if (!existing.Contains(clientId.ToString()))
                    existing.Add(clientId.ToString());
                user.AssignedClientIdsCsv = string.Join(",", existing);
            }

            await db.SaveChangesAsync();
        });

        clientId.Should().NotBe(Guid.Empty, "client seeding failed");
        jobOrderId.Should().NotBe(Guid.Empty, "job order seeding failed");

        // ── POST /api/blue-sticker-reports ────────────────────────────────────
        var coordClient = await _fx.CreateAuthenticatedClient("coordinator@tuv-arabia.local");

        var createResp = await coordClient.PostAsJsonAsync("/api/blue-sticker-reports",
            new CreateBlueStickerReportsRequest(jobOrderId, "ORG-NR", "RPO-NR", "CRM-NR", "NR Contractor", null),
            _json);

        // The bug manifests as HTTP 500 (duplicate-key SQL error); assert OK first.
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"POST /api/blue-sticker-reports returned {(int)createResp.StatusCode}: " +
            $"{await createResp.Content.ReadAsStringAsync()}");

        var reports = await createResp.Content
            .ReadFromJsonAsync<List<BlueStickerReportDetailDto>>(_json);

        // ── Core assertions ───────────────────────────────────────────────────

        reports.Should().NotBeNull("response body must deserialize to a list of reports");

        // The handler fans out to one report per Aramco-categorised equipment (2 seeded).
        reports!.Should().HaveCountGreaterThanOrEqualTo(2,
            "the handler must create one report per Aramco-categorised equipment; " +
            "we seeded 2 such items");

        // Every ReportNo must match the BSR-YYYY-NNNN format.
        reports.Should().AllSatisfy(r =>
            _reportNoRegex.IsMatch(r.ReportNo).Should().BeTrue(
                $"ReportNo '{r.ReportNo}' must match BSR-YYYY-NNNN"));

        // The core regression assertion: ALL report numbers must be DISTINCT.
        // Without the fix the DB throws and we never get here; but guard explicitly too.
        var reportNos = reports.Select(r => r.ReportNo).ToList();
        reportNos.Should().OnlyHaveUniqueItems(
            "every report in the batch must receive a unique sequential report number; " +
            "duplicate numbers indicate the ChangeTracker-pending fix is missing");

        // Every report must start in Draft state.
        reports.Should().AllSatisfy(r =>
            r.State.Should().Be(BlueStickerReportStateDto.Draft,
                "newly created reports must be in Draft state"));

        // ── Optional strength: verify reports are retrievable via GET ─────────
        var listResp = await coordClient.GetAsync(
            $"/api/blue-sticker-reports?jobOrderId={jobOrderId}&pageSize=50");
        listResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/blue-sticker-reports failed: {await listResp.Content.ReadAsStringAsync()}");

        var page = await listResp.Content
            .ReadFromJsonAsync<TuvInspection.Contracts.Common.PagedResult<BlueStickerReportListItemDto>>(_json);
        page.Should().NotBeNull();

        var listedNos = page!.Items.Select(i => i.ReportNo).ToHashSet();
        foreach (var no in reportNos)
        {
            listedNos.Should().Contain(no,
                $"created report '{no}' must appear in GET /api/blue-sticker-reports");
        }
    }
}

[CollectionDefinition("BlueStickerNumbering")]
public sealed class BlueStickerNumberingCollection : ICollectionFixture<BlueStickerNumberingFixture> { }
