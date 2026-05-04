namespace TuvInspection.Application.Certificates;

/// <summary>
/// Events that drive the InspectionCertificate state machine. Each is mapped to one
/// transition path with a role guard inside <see cref="CertificateStateMachine"/>.
/// </summary>
public enum CertificateTrigger
{
    Submit,            // Inspector: Draft|Rejected → Submitted
    BeginReview,       // TechReviewer/Manager: Submitted → UnderReview
    AdvanceForApproval,// TechReviewer: UnderReview → AwaitingApproval
    FinalApprove,      // Manager: AwaitingApproval → Approved
    Reject,            // TechReviewer/Manager: Submitted|UnderReview|AwaitingApproval → Rejected
    Void,              // Manager: any non-terminal post-Approved → Voided
    SendToClient,      // Manager/Coordinator: Approved → ClientSent
    ClientAccept,      // ClientUser: ClientSent → ClientAccepted
    ClientReject,      // ClientUser: ClientSent → ClientRejected → UnderReview
    Archive,           // System: terminal cleanup
    Expire             // System: time-based
}
