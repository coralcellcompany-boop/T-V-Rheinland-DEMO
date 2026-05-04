namespace TuvInspection.Application.Common.Outbox;

/// <summary>
/// Used by command handlers to enqueue side-effects (emails, future notifications) atomically with
/// the business write. The outbox is drained by a hosted background service.
/// </summary>
public interface IOutbox
{
    Task Enqueue<T>(T payload, CancellationToken ct) where T : class;
}
