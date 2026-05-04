namespace TuvInspection.Application.Common.Outbox;

/// <summary>
/// Handles a single outbox payload. Implementations are looked up per outbox message type
/// by the <c>OutboxProcessor</c> hosted service. Each handler should be idempotent — the
/// processor retries on failure with exponential backoff.
/// </summary>
public interface IOutboxMessageHandler<TPayload> where TPayload : class
{
    Task Handle(TPayload payload, CancellationToken ct);
}
