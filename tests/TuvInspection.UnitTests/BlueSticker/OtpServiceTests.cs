using FluentAssertions;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Infrastructure.BlueSticker;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class OtpServiceTests
{
    private static EmailOtpService Svc() => new(outbox: null!, clock: null!);

    [Fact]
    public void Generate_produces_six_digit_code_and_future_expiry()
    {
        var now = new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc);
        var r = Svc().Generate(now, TimeSpan.FromMinutes(15));
        r.Code.Should().MatchRegex(@"^\d{6}$");
        r.ExpiresAtUtc.Should().Be(now.AddMinutes(15));
        r.Hash.Should().NotBeNullOrWhiteSpace().And.NotBe(r.Code);
    }

    [Fact]
    public void Verify_true_for_correct_code()
    {
        var r = Svc().Generate(DateTime.UtcNow, TimeSpan.FromMinutes(15));
        Svc().Verify(r.Code, r.Hash).Should().BeTrue();
    }

    [Fact]
    public void Verify_false_for_wrong_code()
    {
        var r = Svc().Generate(DateTime.UtcNow, TimeSpan.FromMinutes(15));
        Svc().Verify("000000", r.Hash).Should().BeFalse();
    }
}
