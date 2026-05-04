using Stateless;
using TuvInspection.Application.Common;
using TuvInspection.Application.Common.Time;
using TuvInspection.Domain.Assessments;
using TuvInspection.Domain.Identity;

namespace TuvInspection.Application.Assessments;

/// <summary>
/// Operator-assessment lifecycle:
///   Draft → Submitted → Approved
///                     ↘ Rejected → Submitted (resubmit by inspector)
///   Expired (terminal, time-based, system-only)
///
/// The Inspector / TechReviewer creates and submits; Manager approves or rejects.
/// On Approved, the certificate handler auto-issues the competency card (separate step).
/// </summary>
public sealed class AssessmentStateMachine
{
    private readonly Assessment _assessment;
    private readonly ITenantContext _tenant;
    private readonly IClock _clock;
    private readonly StateMachine<AssessmentState, AssessmentTrigger> _fsm;

    private string? _pendingComments;

    public AssessmentStateMachine(Assessment assessment, ITenantContext tenant, IClock clock)
    {
        _assessment = assessment;
        _tenant = tenant;
        _clock = clock;

        _fsm = new StateMachine<AssessmentState, AssessmentTrigger>(
            () => _assessment.State, _ => { });

        Configure();
    }

    public bool CanFire(AssessmentTrigger trigger) => _fsm.CanFire(trigger);

    public void Fire(AssessmentTrigger trigger, string? comments = null)
    {
        _pendingComments = comments;
        _fsm.Fire(trigger);
    }

    private void Configure()
    {
        _fsm.Configure(AssessmentState.Draft)
            .PermitIf(AssessmentTrigger.Submit, AssessmentState.Submitted, IsAssessor,
                "Inspector or TechReviewer required");

        _fsm.Configure(AssessmentState.Submitted)
            .PermitIf(AssessmentTrigger.Approve, AssessmentState.Approved, IsManager, "Manager required")
            .PermitIf(AssessmentTrigger.Reject, AssessmentState.Rejected, IsManager, "Manager required");

        _fsm.Configure(AssessmentState.Rejected)
            .PermitIf(AssessmentTrigger.Resubmit, AssessmentState.Submitted, IsAssessor,
                "Inspector or TechReviewer required");

        _fsm.Configure(AssessmentState.Approved);   // terminal until expired
        _fsm.Configure(AssessmentState.Expired);

        _fsm.OnTransitioned(t =>
        {
            _assessment.ApplyTransition(
                t.Destination,
                _tenant.UserId ?? "system",
                _tenant.PrimaryRole ?? "system",
                _pendingComments,
                _clock.UtcNow,
                Guid.NewGuid());
            _pendingComments = null;
        });
    }

    private bool IsAssessor() =>
        _tenant.IsInRole(Roles.Inspector) ||
        _tenant.IsInRole(Roles.TechReviewer) ||
        _tenant.IsInRole(Roles.Manager);

    private bool IsManager() => _tenant.IsInRole(Roles.Manager);
}
