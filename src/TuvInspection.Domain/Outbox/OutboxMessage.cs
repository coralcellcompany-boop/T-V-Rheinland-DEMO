namespace TuvInspection.Domain.Outbox;

/// <summary>
/// Outbox row — persisted side-effects (emails, notifications) committed in the same
/// transaction as the business change, then dispatched by a hosted background processor.
/// Prevents silent loss of <c>CLIENT_SENT</c> notifications on transient SMTP failure.
/// </summary>
public sealed class OutboxMessage
{
    public Guid Id { get; private set; }
    public string Type { get; private set; } = default!;
    public string PayloadJson { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }
    public DateTime? ProcessedAtUtc { get; private set; }
    public int AttemptCount { get; private set; }
    public string? LastError { get; private set; }
    public DateTime? NextAttemptAtUtc { get; private set; }

    private OutboxMessage() { }

    public OutboxMessage(Guid id, string type, string payloadJson, DateTime createdAtUtc)
    {
        Id = id;
        Type = type ?? throw new ArgumentNullException(nameof(type));
        PayloadJson = payloadJson ?? throw new ArgumentNullException(nameof(payloadJson));
        CreatedAtUtc = createdAtUtc;
        NextAttemptAtUtc = createdAtUtc;
    }

    public void MarkSucceeded(DateTime atUtc)
    {
        ProcessedAtUtc = atUtc;
        LastError = null;
        NextAttemptAtUtc = null;
    }

    public void MarkFailed(DateTime atUtc, string error, TimeSpan retryDelay)
    {
        AttemptCount++;
        LastError = error;
        NextAttemptAtUtc = atUtc + retryDelay;
    }
}
