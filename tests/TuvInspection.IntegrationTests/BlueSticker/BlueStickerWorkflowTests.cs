using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
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
using TuvInspection.Domain.Stickers;
using TuvInspection.Infrastructure.Outbox;
using TuvInspection.Infrastructure.Persistence;
using TuvInspection.Infrastructure.Reports;
using TuvInspection.Infrastructure.Stickers;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;

namespace TuvInspection.IntegrationTests.BlueSticker;

// ─── Fixture ─────────────────────────────────────────────────────────────────

/// <summary>
/// Minimal WebApplicationFactory backed by a dedicated test database on the same dev SQL Server.
/// A unique database name per test run ensures full isolation without spinning a new container.
/// The app is started in "Development" so IdentitySeeder + DevDataSeeder run automatically.
/// </summary>
public sealed class BlueStickerApiFixture : IAsyncLifetime
{
    // Use the existing dev SQL Server but a unique test database per run
    private static readonly string _devServerConn =
        "Server=localhost,1433;User Id=sa;Password=Tuv_Local_Dev_2026!;TrustServerCertificate=True;Encrypt=True";

    private readonly string _dbName = $"TuvInspectionTest_{Guid.NewGuid():N}";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        // Create the test database on the dev SQL Server
        await using (var conn = new Microsoft.Data.SqlClient.SqlConnection(_devServerConn))
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
                // Use the same JWT settings as appsettings so real tokens validate
                b.UseSetting("Jwt:Issuer", "tuv-arabia");
                b.UseSetting("Jwt:Audience", "tuv-arabia-spa");
                b.UseSetting("Jwt:SigningKey", "REPLACE_WITH_AT_LEAST_32_CHAR_DEV_SIGNING_KEY_2026");
                b.UseSetting("Jwt:AccessTokenMinutes", "60");
                // Disable background services that would hit real SMTP / outbox processors in tests
                b.ConfigureServices(services =>
                {
                    // Remove hosted services that fire on startup and would try SMTP or
                    // attempt outbox processing (they time out in a container environment).
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

        // Warm up the factory (runs startup including migrations + seeders)
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        // Flush the EF/ADO.NET connection pool so the DROP DATABASE reliably succeeds —
        // otherwise live connections held by the pool can block the ALTER/DROP.
        SqlConnection.ClearAllPools();

        // Drop the test database
        try
        {
            await using var conn = new SqlConnection(_devServerConn);
            await conn.OpenAsync();
            await using var cmd = conn.CreateCommand();
            cmd.CommandText = $"ALTER DATABASE [{_dbName}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; DROP DATABASE [{_dbName}]";
            await cmd.ExecuteNonQueryAsync();
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[BlueStickerApiFixture] Failed to drop test DB '{_dbName}': {ex.Message}");
        }
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>Creates an authenticated HttpClient for the given role user.</summary>
    public async Task<HttpClient> CreateAuthenticatedClient(string email)
    {
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, TuvInspection.Infrastructure.Identity.DevDataSeeder.DevPassword),
            _jsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Login for {email} failed");
        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>(_jsonOpts);
        login.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    /// <summary>Opens a DI scope and runs an action against AppDbContext.</summary>
    public async Task<T> DbQuery<T>(Func<AppDbContext, Task<T>> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        return await action(db);
    }

    public async Task DbExec(Func<AppDbContext, Task> action)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await action(db);
    }
}

// ─── Test ─────────────────────────────────────────────────────────────────────

[Collection("BlueStickerApi")]
public sealed class BlueStickerWorkflowTests : IClassFixture<BlueStickerApiFixture>
{
    private readonly BlueStickerApiFixture _fx;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public BlueStickerWorkflowTests(BlueStickerApiFixture fx) => _fx = fx;

    [Fact]
    public async Task FullWorkflow_DraftToClientSigned_ProducesValidPdf()
    {
        // ── Seed test prerequisites ───────────────────────────────────────────
        Guid clientId = default;
        Guid jobOrderId = default;

        await _fx.DbExec(async db =>
        {
            // Find an EquipmentType that has an Aramco category (needed for Blue Sticker fan-out)
            var equipType = await db.EquipmentTypes.AsNoTracking()
                .FirstAsync(t => t.AramcoCategory != null && t.AramcoCategory != AramcoCategory.None);

            // Create an isolated test client with a ContactEmail (required for OTP)
            var clientCode = $"TST-{Guid.NewGuid():N}".Substring(0, 12).ToUpperInvariant();
            var client = new Client(Guid.NewGuid(), "Test Aramco Client (E2E)", clientCode)
            {
                CreatedAtUtc = DateTime.UtcNow,
            };
            client.UpdateContact("Test Contact", "+966 50 000 0001", "test.contact@client.example");
            client.SetAllowedServices(ServiceType.BlueSticker);
            db.Clients.Add(client);
            clientId = client.Id;

            // Create one Aramco-categorised equipment item for that client
            var equipment = new EquipmentEntity(
                Guid.NewGuid(), clientId, equipType.Id,
                $"E2E-EQ-{Guid.NewGuid():N}".Substring(0, 16),
                equipType.AramcoCategory)
            {
                CreatedAtUtc = DateTime.UtcNow,
            };
            equipment.UpdateSpec("TestMfr", "TestModel", 2022, "5 t");
            db.Equipment.Add(equipment);

            // Ensure there is at least one unallocated Blue sticker in stock
            var hasStock = await db.Stickers.AnyAsync(s =>
                s.State == StickerState.Unallocated && s.Color == StickerColor.Blue);
            if (!hasStock)
            {
                var sticker = new Sticker(Guid.NewGuid(), "TUVE2E000001", StickerColor.Blue)
                {
                    CreatedAtUtc = DateTime.UtcNow,
                };
                db.Stickers.Add(sticker);
            }

            // Create a BlueSticker job order for the client
            var today = DateOnly.FromDateTime(DateTime.UtcNow);
            var jo = new JobOrder(
                Guid.NewGuid(), $"E2E-JOD-{Guid.NewGuid():N}".Substring(0, 16),
                clientId, ServiceType.BlueSticker, today, today.AddDays(1))
            {
                CreatedAtUtc = DateTime.UtcNow,
            };
            db.JobOrders.Add(jo);
            jobOrderId = jo.Id;

            await db.SaveChangesAsync();

            // Assign the test client to the inspector and coordinator so tenant filter passes
            var staffEmails = new[]
            {
                "coordinator@tuv-arabia.local",
                "inspector1@tuv-arabia.local",
                "techreviewer@tuv-arabia.local",
            };
            foreach (var email in staffEmails)
            {
                var user = await db.Users.FirstOrDefaultAsync(u => u.Email == email);
                if (user is null) continue;
                var existing = string.IsNullOrEmpty(user.AssignedClientIdsCsv)
                    ? new List<string>()
                    : user.AssignedClientIdsCsv.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (!existing.Contains(clientId.ToString()))
                {
                    existing.Add(clientId.ToString());
                    user.AssignedClientIdsCsv = string.Join(",", existing);
                }
            }
            await db.SaveChangesAsync();
        });

        clientId.Should().NotBe(Guid.Empty, "client seeding failed");
        jobOrderId.Should().NotBe(Guid.Empty, "job order seeding failed");

        // ── Build per-role HTTP clients ───────────────────────────────────────
        var coordClient = await _fx.CreateAuthenticatedClient("coordinator@tuv-arabia.local");
        var inspClient = await _fx.CreateAuthenticatedClient("inspector1@tuv-arabia.local");
        var reviewClient = await _fx.CreateAuthenticatedClient("techreviewer@tuv-arabia.local");

        // ── Step 1: Coordinator creates Blue Sticker reports for the job order ─
        var createResp = await coordClient.PostAsJsonAsync("/api/blue-sticker-reports",
            new CreateBlueStickerReportsRequest(jobOrderId, "ORG-001", "RPO-001", "CRM-001", "TUV E2E Contractor", null),
            _json);
        createResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Create reports failed: {await createResp.Content.ReadAsStringAsync()}");

        var reports = await createResp.Content.ReadFromJsonAsync<List<BlueStickerReportDetailDto>>(_json);
        reports.Should().NotBeNullOrEmpty("fan-out should produce at least 1 report for the Aramco equipment");

        var reportId = reports![0].Id;
        reports[0].State.Should().Be(BlueStickerReportStateDto.Draft);

        // ── Step 2: Inspector — StartInspection ───────────────────────────────
        var startResp = await inspClient.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{reportId}/transitions/StartInspection",
            (BlueStickerTransitionRequest?)null, _json);
        startResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"StartInspection failed: {await startResp.Content.ReadAsStringAsync()}");
        var afterStart = await startResp.Content.ReadFromJsonAsync<BlueStickerReportDetailDto>(_json);
        afterStart!.State.Should().Be(BlueStickerReportStateDto.InProgress);

        // ── Step 3: Inspector — fill inspection data ──────────────────────────
        var fillResp = await inspClient.PutAsJsonAsync(
            $"/api/blue-sticker-reports/{reportId}/inspection",
            new UpdateBlueStickerInspectionRequest(
                AreaOfInspection: "Main lifting yard, Bay 3",
                Result: BlueStickerResultDto.Pass,
                Deficiencies: null,
                CorrectiveActionsTaken: null,
                EquipmentLocation: "Yard Bay 3",
                ReceiverName: "Ali Hassan",
                ReceiverBadgeNo: "AB-12345",
                ReceiverTelephone: "+966 50 111 2222",
                InspectorTelephone: "+966 50 333 4444",
                AramcoCategoryNo: null,
                Manufacturer: null,
                Model: null,
                EquipmentType: null,
                EquipmentSerialNo: null,
                Capacity: null),
            _json);
        fillResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Fill inspection failed: {await fillResp.Content.ReadAsStringAsync()}");

        // ── Step 4: Inspector — SubmitForReview ───────────────────────────────
        var submitResp = await inspClient.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{reportId}/transitions/SubmitForReview",
            new BlueStickerTransitionRequest(
                Comments: null,
                InspectorSignaturePng: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg==",
                TechnicalReviewerSignaturePng: null),
            _json);
        submitResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"SubmitForReview failed: {await submitResp.Content.ReadAsStringAsync()}");
        var afterSubmit = await submitResp.Content.ReadFromJsonAsync<BlueStickerReportDetailDto>(_json);
        afterSubmit!.State.Should().Be(BlueStickerReportStateDto.UnderReview);

        // ── Step 5: TechReviewer — Approve (sticker auto-issued, expiry stamped) ─
        var approveResp = await reviewClient.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{reportId}/transitions/Approve",
            new BlueStickerTransitionRequest(
                Comments: "All checks passed.",
                InspectorSignaturePng: null,
                TechnicalReviewerSignaturePng: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="),
            _json);
        approveResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"Approve failed: {await approveResp.Content.ReadAsStringAsync()}");
        var afterApprove = await approveResp.Content.ReadFromJsonAsync<BlueStickerReportDetailDto>(_json);
        afterApprove!.State.Should().Be(BlueStickerReportStateDto.Approved);

        // Sticker auto-issued assertions
        afterApprove.NewStickerNo.Should().NotBeNullOrEmpty(
            "Approve must auto-issue a sticker and stamp its number");
        afterApprove.StickerExpirationDate.Should().NotBeNull(
            "Approve must stamp a sticker expiration date");
        // Interim 1-year validity per Task 13
        afterApprove.StickerExpirationDate!.Value.Should().Be(
            afterApprove.InspectionDate!.Value.AddYears(1),
            "Sticker expiry must be InspectionDate + 1 year (interim validity)");

        // ── Step 6: Inspector — RequestClientOtp ──────────────────────────────
        var otpReqResp = await inspClient.PostAsync(
            $"/api/blue-sticker-reports/{reportId}/request-otp", null);
        otpReqResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"RequestOtp failed: {await otpReqResp.Content.ReadAsStringAsync()}");
        var afterOtpReq = await otpReqResp.Content.ReadFromJsonAsync<BlueStickerReportDetailDto>(_json);
        afterOtpReq!.State.Should().Be(BlueStickerReportStateDto.AwaitingClientSignature);

        // ── Step 7: Capture OTP from OutboxMessages ───────────────────────────
        // The OTP plaintext is stored in the OutboxMessages.PayloadJson as ClientOtpEmail.Code.
        // The Type column is the full CLR type name of ClientOtpEmail.
        const string otpEmailType = "TuvInspection.Infrastructure.Outbox.ClientOtpEmail";
        string otpCode = await _fx.DbQuery(async db =>
        {
            // Give SaveChangesAsync in the handler a brief moment if needed (it's already saved
            // before the HTTP response returned, so this is just defensive).
            var msg = await db.OutboxMessages
                .Where(m => m.Type == otpEmailType && m.ProcessedAtUtc == null)
                .OrderByDescending(m => m.CreatedAtUtc)
                .FirstOrDefaultAsync();

            msg.Should().NotBeNull(
                "OutboxMessages must contain a ClientOtpEmail row after request-otp");

            var payload = JsonSerializer.Deserialize<ClientOtpEmailPayload>(
                msg!.PayloadJson, _json);
            payload.Should().NotBeNull("PayloadJson must deserialize to ClientOtpEmail");
            payload!.ReportId.Should().Be(reportId);
            payload.Code.Should().MatchRegex(@"^\d{6}$", "OTP must be a 6-digit string");
            return payload.Code;
        });

        otpCode.Should().NotBeNullOrEmpty();

        // ── Step 8: Inspector — VerifyOtpAndSign ──────────────────────────────
        var signResp = await inspClient.PostAsJsonAsync(
            $"/api/blue-sticker-reports/{reportId}/verify-and-sign",
            new VerifyOtpAndSignRequest(
                Otp: otpCode,
                ReceiverSignaturePng: "data:image/png;base64,iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mNk+M9QDwADhgGAWjR9awAAAABJRU5ErkJggg=="),
            _json);
        signResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"VerifyAndSign failed: {await signResp.Content.ReadAsStringAsync()}");
        var afterSign = await signResp.Content.ReadFromJsonAsync<BlueStickerReportDetailDto>(_json);

        // ── Step 9: Assert final state ────────────────────────────────────────
        afterSign!.State.Should().Be(BlueStickerReportStateDto.ClientSigned,
            "Report must reach ClientSigned after verify-and-sign");
        afterSign.ReceivedDate.Should().NotBeNull(
            "ReceivedDate must be stamped when client signs");
        afterSign.ReceiverSignaturePng.Should().StartWith("data:image/",
            "ReceiverSignaturePng must be set after sign");

        // ── Step 10: GET report.pdf must return %PDF bytes ────────────────────
        var pdfResp = await inspClient.GetAsync(
            $"/api/blue-sticker-reports/{reportId}/report.pdf");
        pdfResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET report.pdf failed: {pdfResp.StatusCode}");
        pdfResp.Content.Headers.ContentType?.MediaType.Should().Be("application/pdf");

        var pdfBytes = await pdfResp.Content.ReadAsByteArrayAsync();
        pdfBytes.Should().HaveCountGreaterThanOrEqualTo(5, "PDF must be non-trivial");
        // %PDF magic bytes: 0x25 0x50 0x44 0x46
        pdfBytes[0].Should().Be(0x25, "first byte must be '%'");
        pdfBytes[1].Should().Be(0x50, "second byte must be 'P'");
        pdfBytes[2].Should().Be(0x44, "third byte must be 'D'");
        pdfBytes[3].Should().Be(0x46, "fourth byte must be 'F'");
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Minimal projection of the ClientOtpEmail outbox payload to extract the OTP code.
    /// Mirrors the record in TuvInspection.Infrastructure.Outbox.ClientOtpEmailHandler.
    /// </summary>
    private sealed record ClientOtpEmailPayload(
        Guid ReportId,
        string ToEmail,
        string Code,
        DateTime ExpiresAtUtc,
        DateTime AtUtc);
}

[CollectionDefinition("BlueStickerApi")]
public sealed class BlueStickerApiCollection : ICollectionFixture<BlueStickerApiFixture> { }
