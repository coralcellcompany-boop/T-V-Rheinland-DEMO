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

    /// <summary>Constant-time check of a candidate code against a stored hash.</summary>
    bool Verify(string candidate, string storedHash);

    /// <summary>Deliver the code to the client by email (enqueues an outbox message).</summary>
    Task SendAsync(string toEmail, string code, Guid reportId, CancellationToken ct);
}
