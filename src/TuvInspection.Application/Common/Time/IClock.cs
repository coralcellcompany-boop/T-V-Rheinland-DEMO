namespace TuvInspection.Application.Common.Time;

/// <summary>
/// Abstracts <c>DateTime.UtcNow</c> so handlers and the state machine remain deterministic in tests.
/// All persisted timestamps are UTC; Asia/Riyadh (UTC+3) is presentation only.
/// </summary>
public interface IClock
{
    DateTime UtcNow { get; }
}
