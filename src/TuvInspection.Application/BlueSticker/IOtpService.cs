namespace TuvInspection.Application.BlueSticker;

public sealed record OtpGenerationResult(string Code, string Hash, DateTime ExpiresAtUtc);

/// <summary>
/// Generates and verifies one-time codes. Email delivery is the only channel today;
/// the abstraction lets an SMS implementation slot in later without touching callers.
/// </summary>
public interface IOtpService
{
    /// <summary>Create a 6-digit code + its storable hash + expiry. Does NOT send.</summary>
    OtpGenerationResult Generate(DateTime nowUtc, TimeSpan validFor);

    /// <summary>
    /// Constant-time check of a candidate code against a stored hash.
    /// Surrounding whitespace on <paramref name="candidate"/> is trimmed before comparison.
    /// </summary>
    bool Verify(string candidate, string storedHash);

    /// <summary>
    /// Deliver the code to the client by email (enqueues an outbox message).
    /// </summary>
    /// <param name="toEmail">Recipient email address.</param>
    /// <param name="code">The plaintext 6-digit OTP to deliver.</param>
    /// <param name="expiresAtUtc">The exact UTC instant at which the code expires; rendered in the email body.</param>
    /// <param name="reportId">Inspection report the OTP is associated with.</param>
    /// <param name="ct">Cancellation token.</param>
    Task SendAsync(string toEmail, string code, DateTime expiresAtUtc, Guid reportId, CancellationToken ct);
}
