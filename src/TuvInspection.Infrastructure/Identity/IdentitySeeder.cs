using System.Security.Cryptography;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using TuvInspection.Domain.Identity;
using TuvInspection.Infrastructure.Equipment;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Identity;

public sealed class IdentitySeeder
{
    private readonly RoleManager<ApplicationRole> _roles;
    private readonly UserManager<ApplicationUser> _users;
    private readonly AppDbContext _db;
    private readonly AuditDbContext _audit;
    private readonly ILogger<IdentitySeeder> _log;

    public IdentitySeeder(
        RoleManager<ApplicationRole> roles,
        UserManager<ApplicationUser> users,
        AppDbContext db,
        AuditDbContext audit,
        ILogger<IdentitySeeder> log)
    {
        _roles = roles;
        _users = users;
        _db = db;
        _audit = audit;
        _log = log;
    }

    public async Task SeedAsync(CancellationToken ct)
    {
        // Apply migrations for both contexts. In production these would run as a separate
        // pipeline step against an idempotent script — never at startup. For dev convenience
        // we apply them here.
        await _db.Database.MigrateAsync(ct);
        await _audit.Database.MigrateAsync(ct);

        // Seed master data (idempotent).
        await EquipmentTypeSeed.SeedAsync(_db, ct);
        await DefectCodeSeed.SeedAsync(_db, ct);

        // Roles
        foreach (var role in Roles.All)
        {
            if (!await _roles.RoleExistsAsync(role))
                await _roles.CreateAsync(new ApplicationRole { Name = role });
        }

        // Admin user (Manager) — generated password printed once
        const string adminEmail = "admin@tuv-arabia.local";
        var admin = await _users.FindByEmailAsync(adminEmail);
        if (admin is null)
        {
            admin = new ApplicationUser
            {
                UserName = adminEmail,
                Email = adminEmail,
                EmailConfirmed = true,
                FullName = "System Administrator",
                IsActive = true
            };
            var password = GeneratePassword();
            var result = await _users.CreateAsync(admin, password);
            if (!result.Succeeded)
            {
                _log.LogError("Admin seed failed: {Errors}",
                    string.Join("; ", result.Errors.Select(e => e.Description)));
                return;
            }
            _log.LogWarning(
                "==========================================================");
            _log.LogWarning("First-run admin credentials:");
            _log.LogWarning("  Email:    {Email}", adminEmail);
            _log.LogWarning("  Password: {Password}", password);
            _log.LogWarning("CHANGE THIS PASSWORD IMMEDIATELY VIA THE UI.");
            _log.LogWarning(
                "==========================================================");
        }

        // Always ensure admin has all five roles (idempotent — dev convenience so the seed
        // user can drive the full certificate state machine end-to-end).
        if (admin is not null)
        {
            var current = await _users.GetRolesAsync(admin);
            var missing = Roles.All.Except(current).ToList();
            if (missing.Count > 0)
                await _users.AddToRolesAsync(admin, missing);
        }
    }

    private static string GeneratePassword()
    {
        // 16 chars: 4 each of upper/lower/digit/special — meets policy.
        const string upper = "ABCDEFGHJKMNPQRSTUVWXYZ";
        const string lower = "abcdefghijkmnpqrstuvwxyz";
        const string digits = "23456789";
        const string special = "!@#$%^&*+=";
        Span<char> buf = stackalloc char[16];
        FillFrom(upper, buf[..4]);
        FillFrom(lower, buf[4..8]);
        FillFrom(digits, buf[8..12]);
        FillFrom(special, buf[12..16]);
        // Fisher-Yates shuffle
        for (int i = buf.Length - 1; i > 0; i--)
        {
            int j = RandomNumberGenerator.GetInt32(i + 1);
            (buf[i], buf[j]) = (buf[j], buf[i]);
        }
        return new string(buf);
    }

    private static void FillFrom(string alphabet, Span<char> dest)
    {
        for (int i = 0; i < dest.Length; i++)
            dest[i] = alphabet[RandomNumberGenerator.GetInt32(alphabet.Length)];
    }
}
