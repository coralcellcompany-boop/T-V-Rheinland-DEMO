using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Assessments;

public class AssessmentTransition : Entity<Guid>
{
    public Guid AssessmentId { get; private set; }
    public AssessmentState FromState { get; private set; }
    public AssessmentState ToState { get; private set; }
    public string ActorUserId { get; private set; } = default!;
    public string ActorRole { get; private set; } = default!;
    public string? Comments { get; private set; }
    public DateTime AtUtc { get; private set; }

    private AssessmentTransition() { }

    public AssessmentTransition(
        Guid id, Guid assessmentId,
        AssessmentState from, AssessmentState to,
        string actorUserId, string actorRole, string? comments, DateTime atUtc) : base(id)
    {
        AssessmentId = assessmentId;
        FromState = from;
        ToState = to;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Comments = comments;
        AtUtc = atUtc;
    }
}
