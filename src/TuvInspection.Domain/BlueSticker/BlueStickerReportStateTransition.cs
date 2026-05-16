using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.BlueSticker;

/// <summary>Append-only audit row recording every state transition on a BlueStickerReport.</summary>
public class BlueStickerReportStateTransition : Entity<Guid>
{
    public Guid ReportId { get; private set; }
    public BlueStickerReportState FromState { get; private set; }
    public BlueStickerReportState ToState { get; private set; }
    public string ActorUserId { get; private set; } = default!;
    public string ActorRole { get; private set; } = default!;
    public string? Comments { get; private set; }
    public DateTime AtUtc { get; private set; }

    private BlueStickerReportStateTransition() { }

    public BlueStickerReportStateTransition(
        Guid id,
        Guid reportId,
        BlueStickerReportState from,
        BlueStickerReportState to,
        string actorUserId,
        string actorRole,
        string? comments,
        DateTime atUtc) : base(id)
    {
        ReportId = reportId;
        FromState = from;
        ToState = to;
        ActorUserId = actorUserId;
        ActorRole = actorRole;
        Comments = comments;
        AtUtc = atUtc;
    }
}
