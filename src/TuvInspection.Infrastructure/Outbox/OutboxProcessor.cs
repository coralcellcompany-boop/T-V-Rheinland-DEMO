using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Outbox;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Outbox;

/// <summary>
/// Polls the OutboxMessages table every few seconds and dispatches unprocessed messages to a
/// type-keyed <see cref="IOutboxMessageHandler{T}"/>. On exception, increments AttemptCount,
/// stores the error, and schedules a retry with exponential backoff (capped at 1 hour).
/// </summary>
public sealed class OutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<OutboxProcessor> _log;

    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(5);
    private const int MaxAttempts = 8;

    public OutboxProcessor(IServiceScopeFactory scopes, ILogger<OutboxProcessor> log)
    {
        _scopes = scopes;
        _log = log;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _log.LogInformation("Outbox processor started; poll interval {Poll}.", PollInterval);
        while (!stoppingToken.IsCancellationRequested)
        {
            try { await Drain(stoppingToken); }
            catch (Exception ex) { _log.LogError(ex, "Outbox drain pass failed."); }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (TaskCanceledException) { break; }
        }
    }

    private async Task Drain(CancellationToken ct)
    {
        await using var scope = _scopes.CreateAsyncScope();
        var sp = scope.ServiceProvider;
        var db = sp.GetRequiredService<AppDbContext>();
        var clock = sp.GetRequiredService<IClock>();

        var now = clock.UtcNow;
        var ready = await db.OutboxMessages
            .Where(m => m.ProcessedAtUtc == null
                && (m.NextAttemptAtUtc == null || m.NextAttemptAtUtc <= now)
                && m.AttemptCount < MaxAttempts)
            .OrderBy(m => m.CreatedAtUtc)
            .Take(20)
            .ToListAsync(ct);

        foreach (var msg in ready)
        {
            try
            {
                await DispatchOne(sp, msg, ct);
                msg.MarkSucceeded(clock.UtcNow);
                _log.LogInformation("Outbox {Id} {Type} dispatched.", msg.Id, msg.Type);
            }
            catch (Exception ex)
            {
                var delay = ComputeRetryDelay(msg.AttemptCount + 1);
                msg.MarkFailed(clock.UtcNow, ex.Message, delay);
                _log.LogWarning(ex,
                    "Outbox {Id} {Type} attempt {Attempt} failed; retry in {Delay}.",
                    msg.Id, msg.Type, msg.AttemptCount, delay);
            }
        }

        if (ready.Count > 0) await db.SaveChangesAsync(ct);
    }

    private static async Task DispatchOne(IServiceProvider sp, OutboxMessage msg, CancellationToken ct)
    {
        var payloadType = Type.GetType(msg.Type)
            ?? AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => a.GetTypes())
                .FirstOrDefault(t => t.FullName == msg.Type)
            ?? throw new InvalidOperationException($"Unknown outbox payload type '{msg.Type}'.");

        var payload = JsonSerializer.Deserialize(msg.PayloadJson, payloadType)
            ?? throw new InvalidOperationException("Outbox payload deserialized to null.");

        var handlerType = typeof(IOutboxMessageHandler<>).MakeGenericType(payloadType);
        var handler = sp.GetService(handlerType)
            ?? throw new InvalidOperationException(
                $"No IOutboxMessageHandler<{payloadType.Name}> registered.");

        var method = handlerType.GetMethod("Handle")!;
        var task = (Task)method.Invoke(handler, new[] { payload, (object)ct })!;
        await task.ConfigureAwait(false);
    }

    private static TimeSpan ComputeRetryDelay(int attempt)
    {
        var seconds = Math.Min(3600, Math.Pow(2, attempt) * 5);
        return TimeSpan.FromSeconds(seconds);
    }
}
