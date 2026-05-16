using System.Security.Cryptography;
using System.Text;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common.Outbox;
using TuvInspection.Application.Common.Time;
using TuvInspection.Infrastructure.Outbox;

namespace TuvInspection.Infrastructure.BlueSticker;

/// <summary>OTP via the existing email outbox. SHA-256(code+salt) is stored, never the code.</summary>
public sealed class EmailOtpService : IOtpService
{
    private const string Salt = "tuv-bluesticker-otp-v1";
    private readonly IOutbox _outbox;
    private readonly IClock _clock;

    public EmailOtpService(IOutbox outbox, IClock clock)
    {
        _outbox = outbox;
        _clock = clock;
    }

    public OtpGenerationResult Generate(DateTime nowUtc, TimeSpan validFor)
    {
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        return new OtpGenerationResult(code, Hash(code), nowUtc.Add(validFor));
    }

    public bool Verify(string candidate, string storedHash)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(storedHash))
            return false;
        var a = Encoding.UTF8.GetBytes(Hash(candidate.Trim()));
        var b = Encoding.UTF8.GetBytes(storedHash);
        return CryptographicOperations.FixedTimeEquals(a, b);
    }

    public Task SendAsync(string toEmail, string code, DateTime expiresAtUtc, Guid reportId, CancellationToken ct) =>
        _outbox.Enqueue(new ClientOtpEmail(reportId, toEmail, code, expiresAtUtc, _clock.UtcNow), ct);

    private static string Hash(string code)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(code + Salt));
        return Convert.ToHexString(bytes);
    }
}
