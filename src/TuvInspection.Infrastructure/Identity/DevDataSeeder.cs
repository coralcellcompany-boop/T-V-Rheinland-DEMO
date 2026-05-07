using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TuvInspection.Domain.Clients;
using TuvInspection.Domain.Identity;
using TuvInspection.Domain.Stickers;
using TuvInspection.Infrastructure.Persistence;
using EquipmentEntity = TuvInspection.Domain.Equipment.Equipment;

namespace TuvInspection.Infrastructure.Identity;

/// <summary>
/// Dev-only seeder that creates a realistic working dataset for manual testing:
/// inspectors, coordinator, tech reviewer, clients, equipment, sticker stock, and a
/// few pending sticker requests so the approve→assign workflow can be exercised
/// without manually creating users.
///
/// Idempotent. Skipped in non-development environments. All seeded users share the
/// password "Tuv_Dev_2026!".
/// </summary>
public sealed class DevDataSeeder
{
    public const string DevPassword = "Tuv_Dev_2026!";

    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly IHostEnvironment _env;
    private readonly ILogger<DevDataSeeder> _log;

    public DevDataSeeder(
        UserManager<ApplicationUser> users,
        AppDbContext db,
        IHostEnvironment env,
        ILogger<DevDataSeeder> log)
    {
        _users = users;
        _db = db;
        _env = env;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        if (!_env.IsDevelopment())
        {
            _log.LogInformation("DevDataSeeder skipped (environment is {Env}).", _env.EnvironmentName);
            return;
        }

        var inspectors = await SeedUsers(ct);
        var clients = await SeedClients(ct);
        await SeedEquipment(clients, ct);
        await SeedStickerStock(ct);
        await SeedStickerRequests(inspectors, ct);

        _log.LogWarning("==========================================================");
        _log.LogWarning("Dev data seeded. Login with any of these accounts:");
        _log.LogWarning("  Manager     : admin@tuv-arabia.local            (password printed once at first run)");
        _log.LogWarning("  Coordinator : coordinator@tuv-arabia.local      / {Pwd}", DevPassword);
        _log.LogWarning("  TechReviewer: techreviewer@tuv-arabia.local     / {Pwd}", DevPassword);
        _log.LogWarning("  Inspector   : inspector1@tuv-arabia.local       / {Pwd}", DevPassword);
        _log.LogWarning("  Inspector   : inspector2@tuv-arabia.local       / {Pwd}", DevPassword);
        _log.LogWarning("  Inspector   : inspector3@tuv-arabia.local       / {Pwd}", DevPassword);
        _log.LogWarning("==========================================================");
    }

    private async Task<List<ApplicationUser>> SeedUsers(CancellationToken ct)
    {
        var seeded = new List<(string Email, string FullName, string Role)>
        {
            ("coordinator@tuv-arabia.local",  "Layla Al-Otaibi",     Roles.Coordinator),
            ("techreviewer@tuv-arabia.local", "Khalid Al-Mansour",   Roles.TechReviewer),
            ("inspector1@tuv-arabia.local",   "Ahmed Al-Saadi",      Roles.Inspector),
            ("inspector2@tuv-arabia.local",   "Omar Al-Harbi",       Roles.Inspector),
            ("inspector3@tuv-arabia.local",   "Faisal Al-Qahtani",   Roles.Inspector),
        };

        var inspectors = new List<ApplicationUser>();
        foreach (var (email, fullName, role) in seeded)
        {
            var existing = await _users.FindByEmailAsync(email);
            if (existing is null)
            {
                var u = new ApplicationUser
                {
                    UserName = email,
                    Email = email,
                    EmailConfirmed = true,
                    FullName = fullName,
                    IsActive = true,
                };
                var result = await _users.CreateAsync(u, DevPassword);
                if (!result.Succeeded)
                {
                    _log.LogError("Dev user {Email} create failed: {Errors}",
                        email, string.Join("; ", result.Errors.Select(e => e.Description)));
                    continue;
                }
                await _users.AddToRoleAsync(u, role);
                existing = u;
            }
            else
            {
                var current = await _users.GetRolesAsync(existing);
                if (!current.Contains(role))
                    await _users.AddToRoleAsync(existing, role);
            }

            if (role == Roles.Inspector) inspectors.Add(existing);
        }

        return inspectors;
    }

    private async Task<List<Client>> SeedClients(CancellationToken ct)
    {
        var defs = new[]
        {
            ("Aramco Operations Co.",     "ARAMCO",   "Eastern Province, Dhahran",      "Mohammed Al-Rashid", "+966 13 555 0101", "ops@aramco.example",
                ContractStatus.Active,    ServiceType.All),
            ("Sabic Industrial Services", "SABIC",    "Jubail Industrial City",         "Sara Al-Dossari",    "+966 13 555 0202", "ops@sabic.example",
                ContractStatus.Active,    ServiceType.All),
            ("Yanbu Refinery LLC",        "YANBU",    "Yanbu Industrial City",          "Fahad Al-Qurayshi",  "+966 14 555 0303", "ops@yanbu.example",
                ContractStatus.Active,    ServiceType.ThirdPartyInspection | ServiceType.BlueSticker),
            ("Asconcom Contracting Co.",  "ASCO",     "Riyadh, Industrial Area 2",      "Operations Manager", "+966 11 555 0404", "ops@asconcom.com",
                ContractStatus.Suspended, ServiceType.ThirdPartyInspection),
        };

        var clients = new List<Client>();
        foreach (var (name, code, addr, contact, phone, email, status, services) in defs)
        {
            var existing = await _db.Clients.IgnoreQueryFilters()
                .FirstOrDefaultAsync(c => c.Code == code, ct);
            if (existing is null)
            {
                var c = new Client(Guid.NewGuid(), name, code)
                {
                    CreatedAtUtc = DateTime.UtcNow,
                };
                c.UpdateAddress(addr);
                c.UpdateContact(contact, phone, email);
                c.SetContractStatus(status);
                c.SetAllowedServices(services);
                _db.Clients.Add(c);
                clients.Add(c);
            }
            else
            {
                clients.Add(existing);
            }
        }

        await _db.SaveChangesAsync(ct);
        return clients;
    }

    private async Task SeedEquipment(List<Client> clients, CancellationToken ct)
    {
        var anyEquipment = await _db.Equipment.IgnoreQueryFilters().AnyAsync(ct);
        if (anyEquipment) return;

        // Pick a few common equipment types — names match EquipmentTypeSeed.
        var types = await _db.EquipmentTypes.AsNoTracking()
            .Where(t => new[] { "Mobile Crane (Telescopic Boom)", "Forklift Truck", "Chain Sling", "Manbasket", "Air Compressor" }
                .Contains(t.Name))
            .ToListAsync(ct);

        if (types.Count == 0) return;

        var i = 0;
        foreach (var client in clients.Take(3))
        {
            for (var k = 0; k < 4; k++)
            {
                var t = types[i++ % types.Count];
                var e = new EquipmentEntity(
                    Guid.NewGuid(),
                    client.Id,
                    t.Id,
                    $"{client.Code}-EQ-{k + 1:D3}",
                    t.AramcoCategory)
                {
                    CreatedAtUtc = DateTime.UtcNow,
                };
                e.UpdateSpec(
                    manufacturer: k % 2 == 0 ? "Liebherr" : "Caterpillar",
                    model: $"M-{1000 + k * 11}",
                    year: 2018 + k,
                    swl: t.AramcoCategory is null ? null : "5 t");
                e.UpdateLocation($"{client.Address} — Bay {k + 1}");
                _db.Equipment.Add(e);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedStickerStock(CancellationToken ct)
    {
        var existing = await _db.Stickers.AsNoTracking().CountAsync(ct);
        if (existing >= 50) return;

        // Find the highest sticker number to continue the sequence.
        var highest = await _db.Stickers.AsNoTracking()
            .OrderByDescending(s => s.StickerNo)
            .Select(s => s.StickerNo)
            .FirstOrDefaultAsync(ct);
        var seed = 0;
        if (!string.IsNullOrEmpty(highest))
        {
            var digits = new string(highest.Where(char.IsDigit).ToArray());
            int.TryParse(digits, out seed);
        }

        // Procure a mixed batch: 80 Blue, 20 Green, 10 Red, 10 White.
        var spec = new[] { (StickerColor.Blue, 80), (StickerColor.Green, 20), (StickerColor.Red, 10), (StickerColor.White, 10) };
        foreach (var (color, count) in spec)
        {
            for (var k = 0; k < count; k++)
            {
                seed++;
                var sticker = new Sticker(Guid.NewGuid(), $"TUVR{seed:D6}", color)
                {
                    CreatedAtUtc = DateTime.UtcNow,
                };
                _db.Stickers.Add(sticker);
            }
        }

        await _db.SaveChangesAsync(ct);
    }

    private async Task SeedStickerRequests(List<ApplicationUser> inspectors, CancellationToken ct)
    {
        if (inspectors.Count == 0) return;

        var anyRequest = await _db.StickerRequests.AsNoTracking().AnyAsync(ct);
        if (anyRequest) return;

        var year = DateTime.UtcNow.Year;
        var samples = new (ApplicationUser Inspector, StickerColor Color, int Quantity, string Justification)[]
        {
            (inspectors[0], StickerColor.Blue, 25, "JOD2026-0042 — 3-day visit at Yanbu refinery, lifting equipment campaign."),
            (inspectors[1], StickerColor.Blue, 15, "Sabic Jubail site survey — 12 cranes scheduled."),
            (inspectors.Count > 2 ? inspectors[2] : inspectors[0],
                              StickerColor.Green, 10, "Aramco green-tagged forklifts batch."),
        };

        var seq = 0;
        foreach (var (inspector, color, qty, just) in samples)
        {
            seq++;
            var no = $"SR-{year}-{seq:D4}";
            var r = new StickerRequest(Guid.NewGuid(), no, inspector.Id, color, qty, just)
            {
                CreatedAtUtc = DateTime.UtcNow.AddMinutes(-30 * seq),
                CreatedById = inspector.Id,
            };
            _db.StickerRequests.Add(r);
        }

        await _db.SaveChangesAsync(ct);
    }
}
