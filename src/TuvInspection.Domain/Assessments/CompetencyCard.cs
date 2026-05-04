using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Assessments;

/// <summary>
/// Issued operator competency card per SRS §5.4.5. Card numbering is <c>TUVR-YY-NNNNNN</c>.
/// </summary>
public class CompetencyCard : AggregateRoot<Guid>, IAuditable
{
    public string CardNo { get; private set; } = default!;
    public Guid AssessmentId { get; private set; }
    public Guid CandidateId { get; private set; }
    public Guid ClientId { get; private set; }
    public CompetencyCategory Category { get; private set; }
    public DateOnly IssuedOn { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public CompetencyCardState State { get; private set; } = CompetencyCardState.Issued;
    public string? StatusReason { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private CompetencyCard() { }

    public CompetencyCard(
        Guid id, string cardNo,
        Guid assessmentId, Guid candidateId, Guid clientId,
        CompetencyCategory category, DateOnly issuedOn, DateOnly? validUntil) : base(id)
    {
        if (string.IsNullOrWhiteSpace(cardNo))
            throw new ArgumentException("Card number required.", nameof(cardNo));
        CardNo = cardNo.Trim().ToUpperInvariant();
        AssessmentId = assessmentId;
        CandidateId = candidateId;
        ClientId = clientId;
        Category = category;
        IssuedOn = issuedOn;
        ValidUntil = validUntil;
    }

    public void MarkLost(string? reason) => Transition(CompetencyCardState.Lost, reason);
    public void Suspend(string? reason) => Transition(CompetencyCardState.Suspended, reason);
    public void Revoke(string? reason) => Transition(CompetencyCardState.Revoked, reason);
    public void Expire() { if (State == CompetencyCardState.Issued) State = CompetencyCardState.Expired; }

    private void Transition(CompetencyCardState target, string? reason)
    {
        if (State is CompetencyCardState.Expired or CompetencyCardState.Revoked)
            throw new InvalidOperationException($"Card is in terminal state {State}.");
        State = target;
        StatusReason = reason?.Trim();
    }
}
