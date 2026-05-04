using Stateless;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Certificates;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Application.Certificates;

/// <summary>
/// Owns the lifecycle rules for an InspectionCertificate. Configured per certificate instance —
/// transitions are role-gated, and every successful trigger results in a recorded
/// <c>CertificateStateTransition</c> on the aggregate.
///
/// Diagram (per SRS §5.3.5):
///   Draft → Submitted → UnderReview → AwaitingApproval → Approved →
///           ClientSent → ClientAccepted → Archived
///                     ↘ ClientRejected ↗ (back to UnderReview)
///           ↘ Rejected → Draft (rework)
///           ↘ Voided   (terminal, Manager only)
///           ↘ Expired  (terminal, time-based)
/// </summary>
public sealed class CertificateStateMachine
{
    private readonly InspectionCertificate _cert;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly StateMachine<CertificateState, CertificateTrigger> _fsm;

    private string? _pendingComments;

    public CertificateStateMachine(InspectionCertificate cert, ITenantContext tenant, IClock clock)
    {
        _cert = cert ?? throw new ArgumentNullException(nameof(cert));
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));

        _fsm = new StateMachine<CertificateState, CertificateTrigger>(
            () => _cert.State,
            // No setter: we apply via the aggregate so it records a transition row.
            _ => { });

        Configure();
    }

    public bool CanFire(CertificateTrigger trigger) => _fsm.CanFire(trigger);

    public void Fire(CertificateTrigger trigger, string? comments = null)
    {
        _pendingComments = comments;
        _fsm.Fire(trigger);
    }

    private void Configure()
    {
        // ---- Draft ----
        _fsm.Configure(CertificateState.Draft)
            .PermitIf(CertificateTrigger.Submit, CertificateState.Submitted, IsInspector,
                "Inspector role required");

        // ---- Submitted ----
        _fsm.Configure(CertificateState.Submitted)
            .PermitIf(CertificateTrigger.BeginReview, CertificateState.UnderReview, IsTechReviewerOrManager,
                "Tech reviewer or manager required")
            .PermitIf(CertificateTrigger.Reject, CertificateState.Rejected, IsTechReviewerOrManager,
                "Tech reviewer or manager required");

        // ---- UnderReview ----
        _fsm.Configure(CertificateState.UnderReview)
            .PermitIf(CertificateTrigger.AdvanceForApproval, CertificateState.AwaitingApproval, IsTechReviewerOrManager,
                "Tech reviewer or manager required")
            .PermitIf(CertificateTrigger.Reject, CertificateState.Rejected, IsTechReviewerOrManager,
                "Tech reviewer or manager required");

        // ---- AwaitingApproval ----
        _fsm.Configure(CertificateState.AwaitingApproval)
            .PermitIf(CertificateTrigger.FinalApprove, CertificateState.Approved, IsManager,
                "Manager required")
            .PermitIf(CertificateTrigger.Reject, CertificateState.Rejected, IsManager,
                "Manager required");

        // ---- Approved ----
        _fsm.Configure(CertificateState.Approved)
            .PermitIf(CertificateTrigger.SendToClient, CertificateState.ClientSent, IsManagerOrCoordinator,
                "Manager or coordinator required")
            .PermitIf(CertificateTrigger.Void, CertificateState.Voided, IsManager,
                "Manager required");

        // ---- ClientSent ----
        _fsm.Configure(CertificateState.ClientSent)
            .PermitIf(CertificateTrigger.ClientAccept, CertificateState.ClientAccepted, IsClientUser,
                "Client user required")
            .PermitIf(CertificateTrigger.ClientReject, CertificateState.ClientRejected, IsClientUser,
                "Client user required");

        // ---- ClientRejected → back into review ----
        _fsm.Configure(CertificateState.ClientRejected)
            .PermitIf(CertificateTrigger.BeginReview, CertificateState.UnderReview, IsTechReviewerOrManager,
                "Tech reviewer or manager required");

        // ---- ClientAccepted → archive (eventual) ----
        _fsm.Configure(CertificateState.ClientAccepted)
            .PermitIf(CertificateTrigger.Archive, CertificateState.Archived, IsManagerOrCoordinator,
                "Manager or coordinator required");

        // ---- Rejected → back to draft for rework ----
        _fsm.Configure(CertificateState.Rejected)
            .PermitIf(CertificateTrigger.Submit, CertificateState.Submitted, IsInspector,
                "Inspector role required");

        // Terminals
        _fsm.Configure(CertificateState.Voided);
        _fsm.Configure(CertificateState.Expired);
        _fsm.Configure(CertificateState.Archived);

        // Apply every successful transition onto the aggregate.
        _fsm.OnTransitioned(t =>
        {
            _cert.ApplyTransition(
                t.Destination,
                _tenant.UserId ?? "system",
                _tenant.PrimaryRole ?? "system",
                _pendingComments,
                _clock.UtcNow,
                Guid.NewGuid());
            _pendingComments = null;
        });
    }

    // ---- guards ----
    private bool IsInspector() => _tenant.IsInRole(Roles.Inspector);
    private bool IsManager() => _tenant.IsInRole(Roles.Manager);
    private bool IsManagerOrCoordinator() => _tenant.IsInRole(Roles.Manager) || _tenant.IsInRole(Roles.Coordinator);
    private bool IsTechReviewerOrManager() => _tenant.IsInRole(Roles.TechReviewer) || _tenant.IsInRole(Roles.Manager);
    private bool IsClientUser() => _tenant.IsInRole(Roles.ClientUser);
}
