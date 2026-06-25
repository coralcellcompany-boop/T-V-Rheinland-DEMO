using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Data.SqlClient;
using TuvInspection.Contracts.Auth;
using TuvInspection.Contracts.Clients;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;
using TuvInspection.Contracts.Users;
using TuvInspection.Infrastructure.Identity;
using TuvInspection.Infrastructure.Outbox;
using TuvInspection.Infrastructure.Reports;
using TuvInspection.Infrastructure.Stickers;

namespace TuvInspection.IntegrationTests.JobManagement;

// ─── Fixture ─────────────────────────────────────────────────────────────────

/// <summary>
/// Minimal WebApplicationFactory backed by a dedicated throwaway SQL Server database on
/// localhost:1433. Mirrors the pattern established in BlueStickerApiFixture.
/// A unique DB per test run provides full isolation without needing a new container.
/// The app is started in "Development" so IdentitySeeder + DevDataSeeder run automatically.
/// </summary>
public sealed class JobOrderApiFixture : IAsyncLifetime
{
    private static readonly string _devServerConn =
        "Server=localhost,1433;User Id=sa;Password=Tuv_Local_Dev_2026!;TrustServerCertificate=True;Encrypt=True";

    private readonly string _dbName = $"TuvInspectionTest_{Guid.NewGuid():N}";

    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    private static readonly JsonSerializerOptions _jsonOpts = new(JsonSerializerDefaults.Web);

    public async Task InitializeAsync()
    {
        // Create the throwaway test database on the dev SQL Server
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
                    // Remove background services that would hit SMTP / outbox in test environment
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

        // Warm up — runs migrations + seeders (DevDataSeeder populates users/clients)
        _ = Factory.CreateClient();
    }

    public async Task DisposeAsync()
    {
        Factory?.Dispose();

        // Flush connection pool so the DROP DATABASE is not blocked by live connections
        SqlConnection.ClearAllPools();

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
            Console.Error.WriteLine($"[JobOrderApiFixture] Failed to drop test DB '{_dbName}': {ex.Message}");
        }
    }

    /// <summary>Creates an authenticated HttpClient by logging in with the given email.</summary>
    public async Task<HttpClient> CreateAuthenticatedClient(string email)
    {
        var client = Factory.CreateClient();
        var resp = await client.PostAsJsonAsync("/api/auth/login",
            new LoginRequest(email, DevDataSeeder.DevPassword),
            _jsonOpts);
        resp.StatusCode.Should().Be(HttpStatusCode.OK, $"Login for {email} failed");
        var login = await resp.Content.ReadFromJsonAsync<LoginResponse>(_jsonOpts);
        login.Should().NotBeNull();
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }
}

// ─── Tests ────────────────────────────────────────────────────────────────────

[Collection("JobOrderApi")]
public sealed class JobOrderAssignmentPersistenceTests : IClassFixture<JobOrderApiFixture>
{
    private readonly JobOrderApiFixture _fx;

    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    public JobOrderAssignmentPersistenceTests(JobOrderApiFixture fx) => _fx = fx;

    /// <summary>
    /// Regression test for the missing ValueComparer on JobOrder._assignedInspectorIds.
    ///
    /// Without a ValueComparer EF Core change-tracking compares the list by reference
    /// (same object as the snapshot) and never detects in-place mutations produced by
    /// AssignInspector / UnassignInspector — so SaveChanges silently emits no UPDATE
    /// and the assignment is lost on the next read.
    ///
    /// This test exercises the real persistence path via HTTP (real SQL Server + EF
    /// change-tracking across a full SaveChanges + fresh GET read) and will FAIL if the
    /// ValueComparer is removed from JobOrderConfiguration.
    /// </summary>
    [Fact]
    public async Task Assigning_inspectors_persists_across_reload()
    {
        var coordClient = await _fx.CreateAuthenticatedClient("coordinator@tuv-arabia.local");

        // ── Step 1: Resolve a seeded client ID ───────────────────────────────
        var clientsResp = await coordClient.GetAsync("/api/clients?pageSize=1");
        clientsResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/clients failed: {await clientsResp.Content.ReadAsStringAsync()}");
        var clientsPage = await clientsResp.Content.ReadFromJsonAsync<PagedResult<ClientListItemDto>>(_json);
        clientsPage.Should().NotBeNull();
        clientsPage!.Items.Should().NotBeEmpty("DevDataSeeder must have seeded at least one client");
        var clientId = clientsPage.Items[0].Id;

        // ── Step 2: Resolve a seeded inspector ID ────────────────────────────
        var inspectorsResp = await coordClient.GetAsync("/api/users/inspectors");
        inspectorsResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/users/inspectors failed: {await inspectorsResp.Content.ReadAsStringAsync()}");
        var inspectors = await inspectorsResp.Content.ReadFromJsonAsync<IReadOnlyList<InspectorLookupDto>>(_json);
        inspectors.Should().NotBeNullOrEmpty("DevDataSeeder must have seeded inspector users");
        var inspectorId = inspectors![0].Id;

        // ── Step 3: Create a new job order ───────────────────────────────────
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var createBody = new CreateJobOrderRequest(
            ClientId: clientId,
            Service: TuvInspection.Contracts.JobManagement.ServiceTypeDto.ThirdPartyInspection,
            DateFrom: today,
            DateTo: today.AddDays(7),
            Location: "Regression Test Site");

        var createResp = await coordClient.PostAsJsonAsync("/api/job-orders", createBody, _json);
        createResp.StatusCode.Should().Be(HttpStatusCode.Created,
            $"POST /api/job-orders failed: {await createResp.Content.ReadAsStringAsync()}");
        var created = await createResp.Content.ReadFromJsonAsync<JobOrderDetailDto>(_json);
        created.Should().NotBeNull();
        var jobOrderId = created!.Id;
        created.AssignedInspectorIds.Should().BeEmpty("new job order must start with no assigned inspectors");

        // ── Step 4: PUT with one inspector assigned ───────────────────────────
        var updateWithInspector = new UpdateJobOrderRequest(
            DateFrom: today,
            DateTo: today.AddDays(7),
            Location: "Regression Test Site",
            Status: JobOrderStatusDto.Open,
            AssignedInspectorIds: new[] { inspectorId });

        var putResp = await coordClient.PutAsJsonAsync($"/api/job-orders/{jobOrderId}", updateWithInspector, _json);
        putResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"PUT /api/job-orders/{jobOrderId} (assign) failed: {await putResp.Content.ReadAsStringAsync()}");

        // ── Step 5: GET a FRESH read — the critical assertion ─────────────────
        // Without the ValueComparer the EF snapshot keeps the same List<string>
        // reference, mutation is invisible to change-tracking, SaveChanges emits no
        // UPDATE, and the GET returns an empty list instead of [inspectorId].
        var getAfterAssignResp = await coordClient.GetAsync($"/api/job-orders/{jobOrderId}");
        getAfterAssignResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/job-orders/{jobOrderId} after assign failed");
        var afterAssign = await getAfterAssignResp.Content.ReadFromJsonAsync<JobOrderDetailDto>(_json);
        afterAssign.Should().NotBeNull();
        afterAssign!.AssignedInspectorIds.Should().ContainSingle(
            "after assigning one inspector the fresh GET must return exactly that inspector")
            .Which.Should().Be(inspectorId);

        // ── Step 6: PUT to unassign (back to empty) ───────────────────────────
        var updateWithNone = new UpdateJobOrderRequest(
            DateFrom: today,
            DateTo: today.AddDays(7),
            Location: "Regression Test Site",
            Status: JobOrderStatusDto.Open,
            AssignedInspectorIds: Array.Empty<string>());

        var putUnassignResp = await coordClient.PutAsJsonAsync($"/api/job-orders/{jobOrderId}", updateWithNone, _json);
        putUnassignResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"PUT /api/job-orders/{jobOrderId} (unassign) failed: {await putUnassignResp.Content.ReadAsStringAsync()}");

        // ── Step 7: GET again — verify unassign also persisted ────────────────
        var getAfterUnassignResp = await coordClient.GetAsync($"/api/job-orders/{jobOrderId}");
        getAfterUnassignResp.StatusCode.Should().Be(HttpStatusCode.OK,
            $"GET /api/job-orders/{jobOrderId} after unassign failed");
        var afterUnassign = await getAfterUnassignResp.Content.ReadFromJsonAsync<JobOrderDetailDto>(_json);
        afterUnassign.Should().NotBeNull();
        afterUnassign!.AssignedInspectorIds.Should().BeEmpty(
            "after unassigning all inspectors the fresh GET must return an empty list");
    }
}

[CollectionDefinition("JobOrderApi")]
public sealed class JobOrderApiCollection : ICollectionFixture<JobOrderApiFixture> { }
