using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Auditing;
using TuvInspection.Domain.Common;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Auditing;

/// <summary>
/// EF interceptor that captures every mutation against the application DB and writes a
/// hash-chained AuditLog row through the <see cref="AuditDbContext"/>. Hash chain:
///   currentHash = SHA256( previousHash || canonicalJson(row) )
/// Tampering with any historical row breaks the chain on the next write.
/// </summary>
public sealed class AuditSaveChangesInterceptor : SaveChangesInterceptor
{
    private readonly AuditDbContext _audit;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;

    private static readonly JsonSerializerOptions CanonicalJson = new()
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public AuditSaveChangesInterceptor(AuditDbContext audit, ITenantContext tenant, IClock clock)
    {
        _audit = audit;
        _tenant = tenant;
        _clock = clock;
    }

    private List<AuditLog> _pending = new();

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken ct = default)
    {
        var ctx = eventData.Context;
        if (ctx is null) return result;

        // Snapshot every business mutation now, while the change tracker still reflects the pending change.
        var changeEntries = ctx.ChangeTracker.Entries()
            .Where(e => e.Entity is not AuditLog
                && (e.State == EntityState.Added
                    || e.State == EntityState.Modified
                    || e.State == EntityState.Deleted))
            .ToList();

        if (changeEntries.Count == 0) return result;

        _pending = new List<AuditLog>(changeEntries.Count);
        var previousHash = await GetLastHash(ct);

        foreach (var entry in changeEntries)
        {
            if (!ShouldAudit(entry)) continue;

            var (action, before, after) = SnapshotChange(entry);
            var entityName = entry.Metadata.ClrType.Name;
            var entityId = ResolveEntityKey(entry);

            var draft = new AuditLog(
                Guid.NewGuid(), entityName, entityId, action,
                _tenant.UserId, _tenant.PrimaryRole, _clock.UtcNow, _tenant.IpAddress,
                before, after, previousHash, currentHash: "");

            var current = ComputeHash(previousHash, draft);
            _pending.Add(new AuditLog(
                draft.Id, draft.EntityName, draft.EntityId, draft.Action,
                draft.ActorUserId, draft.ActorRole, draft.AtUtc, draft.Ip,
                draft.BeforeJson, draft.AfterJson, previousHash, current));
            previousHash = current;
        }
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(
        SaveChangesCompletedEventData eventData, int result, CancellationToken ct = default)
    {
        if (_pending.Count == 0) return result;

        // Persist to the audit DB AFTER the business save commits. Failures here do not roll
        // back the business write — they're surfaced via Serilog and Outbox alerts.
        try
        {
            _audit.AuditLogs.AddRange(_pending);
            await _audit.SaveChangesAsync(ct);
        }
        finally
        {
            _pending.Clear();
        }
        return result;
    }

    private async Task<string> GetLastHash(CancellationToken ct)
    {
        var last = await _audit.AuditLogs
            .OrderByDescending(a => a.AtUtc)
            .Select(a => a.CurrentHash)
            .FirstOrDefaultAsync(ct);
        return last ?? new string('0', 64);
    }

    private static bool ShouldAudit(EntityEntry entry)
    {
        // Audit aggregate roots and any Domain.Common.AggregateRoot subclass.
        // Skip framework tables (Identity user/role link tables, refresh tokens — those are auth infra).
        if (entry.Entity is null) return false;
        var type = entry.Metadata.ClrType;
        // Audit anything in TuvInspection.Domain.* except AuditLog itself (already filtered).
        return type.Namespace?.StartsWith("TuvInspection.Domain.") == true;
    }

    private static (string Action, string? Before, string? After) SnapshotChange(EntityEntry entry)
    {
        var current = SerializeCurrent(entry);
        return entry.State switch
        {
            EntityState.Added => ("Create", null, current),
            EntityState.Modified => ("Update", SerializeOriginal(entry), current),
            EntityState.Deleted => ("Delete", SerializeOriginal(entry), null),
            _ => ("Unknown", null, null)
        };
    }

    private static string SerializeCurrent(EntityEntry entry)
    {
        var dict = new SortedDictionary<string, object?>();
        foreach (var prop in entry.Properties)
            dict[prop.Metadata.Name] = prop.CurrentValue;
        return JsonSerializer.Serialize(dict, CanonicalJson);
    }

    private static string SerializeOriginal(EntityEntry entry)
    {
        var dict = new SortedDictionary<string, object?>();
        foreach (var prop in entry.Properties)
            dict[prop.Metadata.Name] = prop.OriginalValue;
        return JsonSerializer.Serialize(dict, CanonicalJson);
    }

    private static string ResolveEntityKey(EntityEntry entry)
    {
        var key = entry.Metadata.FindPrimaryKey();
        if (key is null) return "?";
        var parts = key.Properties
            .Select(p => entry.Property(p.Name).CurrentValue?.ToString() ?? "")
            .ToArray();
        return string.Join("|", parts);
    }

    private static string ComputeHash(string previousHash, AuditLog row)
    {
        var canonical = JsonSerializer.Serialize(new
        {
            row.Id,
            row.EntityName,
            row.EntityId,
            row.Action,
            row.ActorUserId,
            row.ActorRole,
            row.AtUtc,
            row.Ip,
            row.BeforeJson,
            row.AfterJson
        }, CanonicalJson);

        var bytes = Encoding.UTF8.GetBytes(previousHash + canonical);
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash);
    }
}
