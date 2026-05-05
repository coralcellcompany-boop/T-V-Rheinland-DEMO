namespace TuvInspection.Domain.Stickers;

/// <summary>
/// Lifecycle of a Blue Sticker per SRS §5.6. State transitions are guarded by the aggregate.
/// </summary>
public enum StickerState
{
    /// <summary>Procured but not yet linked to a job or certificate.</summary>
    Unallocated = 0,
    /// <summary>Reserved for a specific Job Order, not yet on equipment.</summary>
    AllocatedToJob = 1,
    /// <summary>Linked to a certificate, physically affixed to equipment.</summary>
    Issued = 2,
    /// <summary>Older sticker; equipment now has a newer one.</summary>
    Replaced = 3,
    /// <summary>Withdrawn (cancelled certificate or printing error).</summary>
    Voided = 4,
    /// <summary>Past Next Inspection Due Date.</summary>
    Expired = 5
}

/// <summary>
/// Sticker colour (per the Aramco / lifting-equipment colour-coding convention used in
/// the field). Blue is the default Blue Sticker; Green/Red/White are used for variants
/// where the contract or category requires a different colour.
/// </summary>
public enum StickerColor
{
    Blue = 0,
    Green = 1,
    Red = 2,
    White = 3
}
