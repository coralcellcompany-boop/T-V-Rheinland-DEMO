using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Certificates;

/// <summary>
/// Append-only audit row recording every state transition on an InspectionCertificate.
/// </summary>
public class CertificateStateTransition : Entity<Guid>
{
    public Guid CertificateId { get; private set; }
    public CertificateState FromState { get; private set; }
    public CertificateState ToState { get; private set; }
    public string ActorUserId { get; private set; } = default!;
    public string ActorRole { get; private set; } = default!;
    public string? Comments { get; private set; }
    public DateTime AtUtc { get; private set; }

    private CertificateStateTransition() { }

    public CertificateStateTransition(
        Guid id,
        Guid certificateId,
        CertificateState from,
        CertificateState to,
        string actorUserId,
        string actorRole,
        string? comments,
        DateTime atUtc) : base(id)
    {
        CertificateId = certificateId;
        FromState = from;
        ToState = to;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Comments = comments;
        AtUtc = atUtc;
    }
}
