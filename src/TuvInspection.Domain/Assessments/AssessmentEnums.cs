namespace TuvInspection.Domain.Assessments;

/// <summary>
/// Operator competency categories per SRS §4.3 / §8.3. Note the 5th slot is reserved for the
/// equipment family TÜV is still confirming (Mobile Elevating Work Platform / Telehandler).
/// </summary>
public enum CompetencyCategory
{
    None = 0,
    MobileCrane = 1,
    Forklift = 2,
    Manlift = 3,
    WheelLoader = 4,
    MewpTelehandler = 5
}

public enum AssessmentState
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4
}

public enum AssessmentResult
{
    NotSet = 0,
    Pass = 1,
    Fail = 2
}

public enum CompetencyCardState
{
    Issued = 0,
    Lost = 1,
    Suspended = 2,
    Expired = 3,
    Revoked = 4
}

public enum AssessmentTrigger
{
    Submit,         // Inspector: Draft → Submitted
    Approve,        // Manager: Submitted → Approved
    Reject,         // Manager: Submitted → Rejected
    Resubmit,       // Inspector: Rejected → Submitted
    Expire          // System: time-based, terminal
}
