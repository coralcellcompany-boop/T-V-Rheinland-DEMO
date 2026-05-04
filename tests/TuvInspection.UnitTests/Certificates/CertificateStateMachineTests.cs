using FluentAssertions;
using TuvInspection.Application.Certificates;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Identity;
using Xunit;

namespace TuvInspection.UnitTests.Certificates;

public class CertificateStateMachineTests
{
    [Fact]
    public void Inspector_can_submit_a_draft()
    {
        var cert = NewDraft();
        var sm = new CertificateStateMachine(cert, Tenant(Roles.Inspector, "u1"), Clock());

        sm.Fire(CertificateTrigger.Submit);

        cert.State.Should().Be(CertificateState.Submitted);
        cert.Transitions.Should().HaveCount(1);
        cert.Transitions.First().ActorRole.Should().Be(Roles.Inspector);
    }

    [Fact]
    public void Coordinator_cannot_submit_a_draft()
    {
        var cert = NewDraft();
        var sm = new CertificateStateMachine(cert, Tenant(Roles.Coordinator, "u1"), Clock());

        var act = () => sm.Fire(CertificateTrigger.Submit);

        act.Should().Throw<InvalidOperationException>("Coordinator role does not satisfy the Submit guard");
        cert.State.Should().Be(CertificateState.Draft);
    }

    [Fact]
    public void Manager_can_final_approve_only_from_AwaitingApproval()
    {
        var cert = NewDraft();
        new CertificateStateMachine(cert, Tenant(Roles.Inspector, "u1"), Clock())
            .Fire(CertificateTrigger.Submit);
        new CertificateStateMachine(cert, Tenant(Roles.TechReviewer, "u2"), Clock())
            .Fire(CertificateTrigger.BeginReview);
        new CertificateStateMachine(cert, Tenant(Roles.TechReviewer, "u2"), Clock())
            .Fire(CertificateTrigger.AdvanceForApproval);

        new CertificateStateMachine(cert, Tenant(Roles.Manager, "u3"), Clock())
            .Fire(CertificateTrigger.FinalApprove, "Approved with no findings.");

        cert.State.Should().Be(CertificateState.Approved);
        cert.Transitions.Should().HaveCount(4);
        cert.Transitions.Last().Comments.Should().Be("Approved with no findings.");
    }

    [Fact]
    public void Inspector_cannot_final_approve()
    {
        var cert = AwaitingApprovalCert();
        var sm = new CertificateStateMachine(cert, Tenant(Roles.Inspector, "u1"), Clock());

        var act = () => sm.Fire(CertificateTrigger.FinalApprove);

        act.Should().Throw<InvalidOperationException>();
        cert.State.Should().Be(CertificateState.AwaitingApproval);
    }

    [Fact]
    public void Rejected_certificate_can_be_resubmitted_by_inspector()
    {
        var cert = NewDraft();
        new CertificateStateMachine(cert, Tenant(Roles.Inspector, "u1"), Clock())
            .Fire(CertificateTrigger.Submit);
        new CertificateStateMachine(cert, Tenant(Roles.TechReviewer, "u2"), Clock())
            .Fire(CertificateTrigger.Reject, "Missing photos");

        cert.State.Should().Be(CertificateState.Rejected);

        new CertificateStateMachine(cert, Tenant(Roles.Inspector, "u1"), Clock())
            .Fire(CertificateTrigger.Submit);

        cert.State.Should().Be(CertificateState.Submitted);
    }

    private static InspectionCertificate NewDraft() =>
        new(
            Guid.NewGuid(),
            "IS-275711-26-7097",
            clientId: Guid.NewGuid(),
            equipmentId: Guid.NewGuid(),
            jobOrderId: null,
            inspectionDate: new DateOnly(2026, 5, 1),
            reportIssueDate: new DateOnly(2026, 5, 1),
            type: CertificateInspectionType.PeriodicInspection);

    private static InspectionCertificate AwaitingApprovalCert()
    {
        var cert = NewDraft();
        new CertificateStateMachine(cert, Tenant(Roles.Inspector, "u1"), Clock())
            .Fire(CertificateTrigger.Submit);
        new CertificateStateMachine(cert, Tenant(Roles.TechReviewer, "u2"), Clock())
            .Fire(CertificateTrigger.BeginReview);
        new CertificateStateMachine(cert, Tenant(Roles.TechReviewer, "u2"), Clock())
            .Fire(CertificateTrigger.AdvanceForApproval);
        return cert;
    }

    private static IClock Clock() => new FixedClock(new DateTime(2026, 5, 4, 10, 0, 0, DateTimeKind.Utc));

    private static ITenantContext Tenant(string role, string userId) =>
        new TestTenantContext(role, userId);

    private sealed class FixedClock : IClock
    {
        public FixedClock(DateTime now) => UtcNow = now;
        public DateTime UtcNow { get; }
    }

    private sealed class TestTenantContext : ITenantContext
    {
        public TestTenantContext(string role, string userId)
        {
            Roles = new HashSet<string> { role };
            UserId = userId;
            PrimaryRole = role;
        }

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
