using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Stickers;

/// <summary>
/// Aggregate root for a single Blue Sticker. Sticker numbers follow the format
/// <c>TUVR######</c> (TUVR + 6 digits). Stickers are first procured into the
/// <see cref="StickerState.Unallocated"/> pool, then either manually issued by a coordinator
/// or auto-issued by the system when an Aramco-categorized certificate is approved.
/// </summary>
public class Sticker : AggregateRoot<Guid>, IAuditable
{
    public string StickerNo { get; private set; } = default!;        // e.g. TUVR000123
    public StickerState State { get; private set; } = StickerState.Unallocated;
    public StickerColor Color { get; private set; } = StickerColor.Blue;

    public Guid? AllocatedToJobOrderId { get; private set; }
    public string? AssignedToInspectorId { get; private set; }
    public DateTime? AssignedAtUtc { get; private set; }
    public Guid? AssignedFromRequestId { get; private set; }

    public Guid? IssuedToCertificateId { get; private set; }
    public Guid? IssuedToEquipmentId { get; private set; }
    public Guid? ClientId { get; private set; }

    public DateTime? IssuedAtUtc { get; private set; }
    public DateOnly? ValidUntil { get; private set; }
    public DateTime? VoidedAtUtc { get; private set; }
    public string? VoidReason { get; private set; }
    public Guid? ReplacedByStickerId { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private Sticker() { }

    public Sticker(Guid id, string stickerNo, StickerColor color = StickerColor.Blue) : base(id)
    {
        if (string.IsNullOrWhiteSpace(stickerNo))
            throw new ArgumentException("Sticker number is required.", nameof(stickerNo));
        StickerNo = stickerNo.Trim().ToUpperInvariant();
        Color = color;
    }

    public void AssignToInspector(string inspectorUserId, Guid? fromRequestId, DateTime atUtc)
    {
        if (State != StickerState.Unallocated)
            throw new InvalidOperationException(
                $"Only Unallocated stickers can be assigned to an inspector. Current state: {State}.");
        if (string.IsNullOrWhiteSpace(inspectorUserId))
            throw new ArgumentException("Inspector required.", nameof(inspectorUserId));
        AssignedToInspectorId = inspectorUserId;
        AssignedAtUtc = atUtc;
        AssignedFromRequestId = fromRequestId;
    }

    public void Unassign()
    {
        AssignedToInspectorId = null;
        AssignedAtUtc = null;
        AssignedFromRequestId = null;
    }

    public void AllocateToJob(Guid jobOrderId)
    {
        if (State != StickerState.Unallocated)
            throw new InvalidOperationException(
                $"Only Unallocated stickers can be allocated. Current state: {State}.");
        AllocatedToJobOrderId = jobOrderId;
        State = StickerState.AllocatedToJob;
    }

    public void Issue(Guid certificateId, Guid equipmentId, Guid clientId,
        DateOnly? validUntil, DateTime atUtc)
    {
        if (State is not (StickerState.Unallocated or StickerState.AllocatedToJob))
            throw new InvalidOperationException(
                $"Cannot issue sticker in state {State}.");
        IssuedToCertificateId = certificateId;
        IssuedToEquipmentId = equipmentId;
        ClientId = clientId;
        ValidUntil = validUntil;
        IssuedAtUtc = atUtc;
        State = StickerState.Issued;
    }

    public void Void(string reason, DateTime atUtc)
    {
        if (State is StickerState.Voided or StickerState.Expired or StickerState.Replaced)
            throw new InvalidOperationException(
                $"Cannot void a sticker in terminal state {State}.");
        State = StickerState.Voided;
        VoidedAtUtc = atUtc;
        VoidReason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
    }

    public void Expire()
    {
        if (State == StickerState.Issued) State = StickerState.Expired;
    }

    public void MarkReplacedBy(Guid replacementId)
    {
        if (State != StickerState.Issued)
            throw new InvalidOperationException(
                $"Only Issued stickers can be replaced. Current state: {State}.");
        ReplacedByStickerId = replacementId;
        State = StickerState.Replaced;
    }
}
