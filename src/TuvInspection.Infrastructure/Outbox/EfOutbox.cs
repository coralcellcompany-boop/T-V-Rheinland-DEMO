using System.Text.Json;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Outbox;
using TuvInspection.Infrastructure.Persistence;

namespace TuvInspection.Infrastructure.Outbox;

public sealed class EfOutbox : IOutbox
{
    private readonly AppDbContext _db;
    private readonly IClock _clock;

    public EfOutbox(AppDbContext db, IClock clock)
    {
        _db = db;
        _clock = clock;
    }

    public Task Enqueue<T>(T payload, CancellationToken ct) where T : class
    {
        var msg = new OutboxMessage(
            Guid.NewGuid(),
            typeof(T).FullName!,
            JsonSerializer.Serialize(payload),
            _clock.UtcNow);
        _db.OutboxMessages.Add(msg);
        return Task.CompletedTask;
    }
}
