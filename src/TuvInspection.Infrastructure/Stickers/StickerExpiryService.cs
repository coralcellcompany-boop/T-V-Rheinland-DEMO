using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Stickers;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Stickers;

/// <summary>
/// Daily sweep that flips <c>Issued</c> stickers whose <c>ValidUntil</c> is in the past into
/// the <c>Expired</c> terminal state. Without this, the public sticker-verification page would
/// continue to display "Valid" beyond the actual inspection due date.
///
/// Runs hourly (cheap, in-process) and only does real work when the day rolls over.
/// </summary>
public sealed class StickerExpiryService : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<StickerExpiryService> _log;

    private DateOnly _lastRun = DateOnly.MinValue;

    public StickerExpiryService(IServiceScopeFactory scopes, ILogger<StickerExpiryService> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Sticker expiry sweep started — hourly poll, runs at most once per day.");

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var clock = scope.ServiceProvider.GetRequiredService<IClock>();
                var today = DateOnly.FromDateTime(clock.UtcNow);
                if (today > _lastRun)
                {
                    var swept = await SweepOne(scope.ServiceProvider, today, clock.UtcNow, stoppingToken);
                    _lastRun = today;
                    if (swept > 0)
                        _log.LogInformation("Expired {Count} sticker(s) on {Date}.", swept, today);
                }
            }
            catch (Exception ex)
            {
                _log.LogError(ex, "Sticker expiry sweep failed.");
            }

            try { await Task.Delay(TimeSpan.FromHours(1), stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private static async Task<int> SweepOne(IServiceProvider sp, DateOnly today, DateTime utcNow,
        CancellationToken ct)
    {
        var db = sp.GetRequiredService<AppDbContext>();
        var due = await db.Stickers
            .Where(s => s.State == StickerState.Issued
                && s.ValidUntil != null
                && s.ValidUntil < today)
            .ToListAsync(ct);

        foreach (var s in due)
        {
            s.Expire();
            s.UpdatedAtUtc = utcNow;
        }
        if (due.Count > 0) await db.SaveChangesAsync(ct);
        return due.Count;
    }
}
