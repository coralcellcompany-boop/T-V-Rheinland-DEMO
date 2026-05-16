using Stateless;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.BlueSticker;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Application.BlueSticker;

/// <summary>
/// Owns the 9-step Blue Sticker lifecycle. Role-gated; every successful trigger records a
/// BlueStickerReportStateTransition on the aggregate.
///
///   Draft → InProgress → UnderReview → Approved → AwaitingClientSignature → ClientSigned
///                            ↘ Reject → InProgress (rework; goes directly, no Rejected hop)
///                            ↘ Voided (terminal)
///
/// Approval is final at the TechReviewer step (Manager may also Approve).
/// </summary>
public sealed class BlueStickerReportStateMachine
{
    private readonly BlueStickerReport _r;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly StateMachine<BlueStickerReportState, BlueStickerReportTrigger> _fsm;
    private string? _pendingComments;

    public BlueStickerReportStateMachine(BlueStickerReport report, ITenantContext tenant, IClock clock)
    {
        _r = report ?? throw new ArgumentNullException(nameof(report));
        _tenant = tenant ?? throw new ArgumentNullException(nameof(tenant));
        _clock = clock ?? throw new ArgumentNullException(nameof(clock));
        _fsm = new StateMachine<BlueStickerReportState, BlueStickerReportTrigger>(
            () => _r.State, _ => { });
        Configure();
    }

    public bool CanFire(BlueStickerReportTrigger t) => _fsm.CanFire(t);

    public void Fire(BlueStickerReportTrigger t, string? comments = null)
    {
        _pendingComments = comments;
        _fsm.Fire(t);
    }

    private void Configure()
    {
        _fsm.Configure(BlueStickerReportState.Draft)
            .PermitIf(BlueStickerReportTrigger.StartInspection, BlueStickerReportState.InProgress,
                IsInspector, "Inspector role required")
            .PermitIf(BlueStickerReportTrigger.Void, BlueStickerReportState.Voided,
                IsManagerOrCoordinator, "Manager or coordinator required");

        _fsm.Configure(BlueStickerReportState.InProgress)
            .PermitIf(BlueStickerReportTrigger.SubmitForReview, BlueStickerReportState.UnderReview,
                IsInspector, "Inspector role required")
            .PermitIf(BlueStickerReportTrigger.Void, BlueStickerReportState.Voided,
                IsManagerOrCoordinator, "Manager or coordinator required");

        // Reject goes directly to InProgress (simpler variant — avoids re-entrant Fire in OnEntry).
        _fsm.Configure(BlueStickerReportState.UnderReview)
            .PermitIf(BlueStickerReportTrigger.Approve, BlueStickerReportState.Approved,
                IsTechReviewerOrManager, "Tech reviewer or manager required")
            .PermitIf(BlueStickerReportTrigger.Reject, BlueStickerReportState.InProgress,
                IsTechReviewerOrManager, "Tech reviewer or manager required");

        _fsm.Configure(BlueStickerReportState.Approved)
            .PermitIf(BlueStickerReportTrigger.RequestClientOtp,
                BlueStickerReportState.AwaitingClientSignature, IsInspector, "Inspector role required")
            .PermitIf(BlueStickerReportTrigger.Void, BlueStickerReportState.Voided,
                IsManager, "Manager required");

        _fsm.Configure(BlueStickerReportState.AwaitingClientSignature)
            .PermitIf(BlueStickerReportTrigger.VerifyOtpAndSign, BlueStickerReportState.ClientSigned,
                IsInspector, "Inspector role required")
            .PermitReentryIf(BlueStickerReportTrigger.RequestClientOtp, IsInspector,
                "Inspector role required"); // resend OTP

        _fsm.Configure(BlueStickerReportState.ClientSigned);
        _fsm.Configure(BlueStickerReportState.Rejected);   // kept as terminal placeholder
        _fsm.Configure(BlueStickerReportState.Voided);

        _fsm.OnTransitioned(t =>
        {
            _r.ApplyTransition(t.Destination, _tenant.UserId ?? "system",
                _tenant.PrimaryRole ?? "system", _pendingComments, _clock.UtcNow, Guid.NewGuid());
            _pendingComments = null;
        });
    }

    // ---- guards ----
    private bool IsInspector() => _tenant.IsInRole(Roles.Inspector);
    private bool IsManager() => _tenant.IsInRole(Roles.Manager);
    private bool IsManagerOrCoordinator() =>
        _tenant.IsInRole(Roles.Manager) || _tenant.IsInRole(Roles.Coordinator);
    private bool IsTechReviewerOrManager() =>
        _tenant.IsInRole(Roles.TechReviewer) || _tenant.IsInRole(Roles.Manager);
}
