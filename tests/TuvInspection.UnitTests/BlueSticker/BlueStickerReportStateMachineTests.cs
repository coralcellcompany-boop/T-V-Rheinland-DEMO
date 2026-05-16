using FluentAssertions;
using TuvInspection.Application.BlueSticker;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Identity;
using Xunit;

namespace TuvInspection.UnitTests.BlueSticker;

public class BlueStickerReportStateMachineTests
{
    [Fact]
    public void Inspector_starts_inspection_from_draft()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        r.State.Should().Be(BlueStickerReportState.InProgress);
        r.Transitions.Should().HaveCount(1);
    }

    [Fact]
    public void Coordinator_cannot_start_inspection()
    {
        var r = NewDraft();
        var act = () => Sm(r, Roles.Coordinator).Fire(BlueStickerReportTrigger.StartInspection);
        act.Should().Throw<InvalidOperationException>();
        r.State.Should().Be(BlueStickerReportState.Draft);
    }

    [Fact]
    public void Full_happy_path_reaches_ClientSigned()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.SubmitForReview);
        r.State.Should().Be(BlueStickerReportState.UnderReview);
        Sm(r, Roles.TechReviewer).Fire(BlueStickerReportTrigger.Approve);
        r.State.Should().Be(BlueStickerReportState.Approved);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.RequestClientOtp);
        r.State.Should().Be(BlueStickerReportState.AwaitingClientSignature);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.VerifyOtpAndSign);
        r.State.Should().Be(BlueStickerReportState.ClientSigned);
    }

    [Fact]
    public void Reviewer_reject_returns_to_InProgress()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.SubmitForReview);
        Sm(r, Roles.TechReviewer).Fire(BlueStickerReportTrigger.Reject, "Fix area of inspection");
        r.State.Should().Be(BlueStickerReportState.InProgress);
    }

    [Fact]
    public void Manager_can_also_approve()
    {
        var r = NewDraft();
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.StartInspection);
        Sm(r, Roles.Inspector).Fire(BlueStickerReportTrigger.SubmitForReview);
        Sm(r, Roles.Manager).Fire(BlueStickerReportTrigger.Approve);
        r.State.Should().Be(BlueStickerReportState.Approved);
    }

    private static BlueStickerReport NewDraft() =>
        new(Guid.NewGuid(), "BSR-2026-0001", Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(),
            "JOD2026-0001", "EQ-1");

    private static BlueStickerReportStateMachine Sm(BlueStickerReport r, string role) =>
        new(r, new TestTenantContext(role, "u-" + role), new FixedClock(
            new DateTime(2026, 5, 16, 9, 0, 0, DateTimeKind.Utc)));

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now) => UtcNow = now;
        public DateTime UtcNow { get; }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string role, string userId)
        { Roles = new HashSet<string> { role }; UserId = userId; PrimaryRole = role; }
        public bool IsAnonymous => false;
        public string? UserId { get; }
        public string? UserName => UserId;
        public string? PrimaryRole { get; }
        public IReadOnlySet<string> Roles { get; }
        public IReadOnlySet<Guid> AssignedClientIds { get; } = new HashSet<Guid>();
        public Guid? ActiveClientId => null;
        public string? IpAddress => null;
        public bool IsInRole(string role) => Roles.Contains(role);
    }
}
