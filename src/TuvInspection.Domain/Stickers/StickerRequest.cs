using TuvInspection.Domain.Common;

namespace TuvInspection.Domain.Stickers;

/// <summary>
/// Inspector-initiated request for a quantity of stickers. Coordinator/Manager approves the
/// request, at which point the system reserves N unallocated stickers (matching the requested
/// colour) and assigns them to the inspector. The inspector then issues those stickers to
/// equipment as part of the certificate approval workflow.
/// </summary>
public class StickerRequest : AggregateRoot<Guid>, IAuditable
{
    public string RequestNo { get; private set; } = default!;     // SR-YYYY-NNNN
    public string InspectorUserId { get; private set; } = default!;
    public StickerColor Color { get; private set; }
    public int Quantity { get; private set; }
    public string? Justification { get; private set; }
    public StickerRequestState State { get; private set; } = StickerRequestState.Pending;

    public string? DecidedByUserId { get; private set; }
    public DateTime? DecidedAtUtc { get; private set; }
    public string? DecisionComments { get; private set; }
    public int AllocatedCount { get; private set; }

    public DateTime CreatedAtUtc { get; set; }
    public string? CreatedById { get; set; }
    public DateTime? UpdatedAtUtc { get; set; }
    public string? UpdatedById { get; set; }

    private StickerRequest() { }

    public StickerRequest(Guid id, string requestNo, string inspectorUserId,
        StickerColor color, int quantity, string? justification) : base(id)
    {
        if (string.IsNullOrWhiteSpace(requestNo)) throw new ArgumentException("Request number required.", nameof(requestNo));
        if (string.IsNullOrWhiteSpace(inspectorUserId)) throw new ArgumentException("Inspector required.", nameof(inspectorUserId));
        if (quantity <= 0 || quantity > 500)
            throw new ArgumentOutOfRangeException(nameof(quantity), "Quantity must be between 1 and 500.");
        RequestNo = requestNo.Trim().ToUpperInvariant();
        InspectorUserId = inspectorUserId;
        Color = color;
        Quantity = quantity;
        Justification = justification?.Trim();
    }

    public void Approve(string approverUserId, string? comments, int allocated, DateTime atUtc)
    {
        if (State != StickerRequestState.Pending)
            throw new InvalidOperationException($"Only Pending requests can be approved. Current state: {State}.");
        State = StickerRequestState.Approved;
        DecidedByUserId = approverUserId;
        DecisionComments = comments?.Trim();
        DecidedAtUtc = atUtc;
        AllocatedCount = allocated;
    }

    public void Reject(string approverUserId, string? comments, DateTime atUtc)
    {
        if (State != StickerRequestState.Pending)
            throw new InvalidOperationException($"Only Pending requests can be rejected. Current state: {State}.");
        State = StickerRequestState.Rejected;
        DecidedByUserId = approverUserId;
        DecisionComments = comments?.Trim();
        DecidedAtUtc = atUtc;
    }

    public void Cancel(DateTime atUtc)
    {
        if (State != StickerRequestState.Pending)
            throw new InvalidOperationException($"Only Pending requests can be cancelled. Current state: {State}.");
        State = StickerRequestState.Cancelled;
        DecidedAtUtc = atUtc;
    }
}

public enum StickerRequestState
{
    Pending = 0,
    Approved = 1,
    Rejected = 2,
    Cancelled = 3
}
