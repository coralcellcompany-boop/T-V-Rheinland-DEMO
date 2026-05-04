using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Assessments;

/// <summary>
/// Aggregate root for an operator competency assessment. Mutability is restricted to the
/// Draft and Rejected states (rework). The state machine itself lives in the Application
/// layer; this aggregate exposes guarded mutators.
/// </summary>
public class Assessment : AggregateRoot<Guid>, IAuditable, ITenantScoped
{
    public string AssessmentNo { get; private set; } = default!;       // ASM2026-NNNN
    public Guid CandidateId { get; private set; }
    public Guid ClientId { get; private set; }
    public CompetencyCategory Category { get; private set; }
    public DateOnly AssessmentDate { get; private set; }
    public DateOnly? NextAssessmentDate { get; private set; }
    public string? Location { get; private set; }
    public Guid? JobOrderId { get; private set; }

    public AssessmentResult Result { get; private set; } = AssessmentResult.NotSet;
    public int? TheoreticalScore { get; private set; }      // out of 100
    public int? PracticalScore { get; private set; }        // out of 100
    public string? Comments { get; private set; }

    public AssessmentState State { get; private set; } = AssessmentState.Draft;

    public Guid? IssuedCardId { get; private set; }
    public string? IssuedCardNo { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private readonly List<AssessmentTransition> _transitions = new();
    public IReadOnlyCollection<AssessmentTransition> Transitions => _transitions.AsReadOnly();

    private Assessment() { }

    public Assessment(
        Guid id,
        string assessmentNo,
        Guid candidateId,
        Guid clientId,
        CompetencyCategory category,
        DateOnly assessmentDate) : base(id)
    {
        if (string.IsNullOrWhiteSpace(assessmentNo))
            throw new ArgumentException("Assessment number required.", nameof(assessmentNo));
        if (category == CompetencyCategory.None)
            throw new ArgumentException("Category required.", nameof(category));

        AssessmentNo = assessmentNo.Trim();
        CandidateId = candidateId;
        ClientId = clientId;
        Category = category;
        AssessmentDate = assessmentDate;
    }

    public void UpdateScores(int? theoretical, int? practical, AssessmentResult result, string? comments,
        DateOnly? nextAssessmentDate, string? location)
    {
        EnsureMutable();
        if (theoretical is < 0 or > 100) throw new ArgumentException("Theoretical score must be 0..100.");
        if (practical is < 0 or > 100) throw new ArgumentException("Practical score must be 0..100.");
        TheoreticalScore = theoretical;
        PracticalScore = practical;
        Result = result;
        Comments = comments?.Trim();
        NextAssessmentDate = nextAssessmentDate;
        Location = location?.Trim();
    }

    public void ApplyTransition(AssessmentState target, string actorUserId, string actorRole,
        string? comments, DateTime atUtc, Guid transitionId)
    {
        _transitions.Add(new AssessmentTransition(
            transitionId, Id, State, target, actorUserId, actorRole, comments, atUtc));
        State = target;
    }

    public void LinkCard(Guid cardId, string cardNo)
    {
        IssuedCardId = cardId;
        IssuedCardNo = cardNo;
    }

    private void EnsureMutable()
    {
        if (State is not (AssessmentState.Draft or AssessmentState.Rejected))
            throw new InvalidOperationException(
                $"Assessment cannot be edited in state {State}. Only Draft and Rejected are mutable.");
    }
}
