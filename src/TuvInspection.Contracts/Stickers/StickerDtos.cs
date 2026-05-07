namespace TuvInspection.Contracts.Stickers;

public enum StickerStateDto
{
    Unallocated = 0,
    AllocatedToJob = 1,
    Issued = 2,
    Replaced = 3,
    Voided = 4,
    Expired = 5
}

public sealed record StickerListItemDto(
    Guid Id,
    string StickerNo,
    StickerStateDto State,
    StickerColorDto Color,
    string? AssignedToInspectorId,
    string? AssignedToInspectorName,
    Guid? ClientId,
    string? ClientName,
    Guid? IssuedToCertificateId,
    string? CertificateNo,
    Guid? IssuedToEquipmentId,
    string? EquipmentIdNo,
    DateOnly? ValidUntil,
    DateTime CreatedAtUtc);

public enum StickerColorDto { Blue = 0, Green = 1, Red = 2, White = 3 }

public enum StickerRequestStateDto { Pending = 0, Approved = 1, Rejected = 2, Cancelled = 3 }

public sealed record StickerRequestDto(
    Guid Id,
    string RequestNo,
    string InspectorUserId,
    string? InspectorName,
    StickerColorDto Color,
    int Quantity,
    string? Justification,
    StickerRequestStateDto State,
    string? DecidedByUserId,
    string? DecidedByName,
    DateTime? DecidedAtUtc,
    string? DecisionComments,
    int AllocatedCount,
    DateTime CreatedAtUtc);

public sealed record CreateStickerRequest(
    StickerColorDto Color,
    int Quantity,
    string? Justification);

public sealed record StickerStockSummaryDto(
    int Unallocated,
    int Issued,
    int Voided,
    int Expired,
    int LowStockThreshold,
    bool IsLowStock);

public sealed record ProcureStockRequest(int Count, StickerColorDto Color = StickerColorDto.Blue);

public sealed record AssignStickersRequest(
    string InspectorUserId,
    StickerColorDto Color,
    int Count);

public sealed record RejectStickerRequestBody(string Reason);

public sealed record DecisionCommentsBody(string? Comments);

public sealed record VoidStickerRequest(string Reason);

/// <summary>Public verification payload — deliberately excludes PII.</summary>
public sealed record StickerPublicViewDto(
    string StickerNo,
    StickerStateDto State,
    string? AramcoCategory,
    string? EquipmentTypeName,
    string? EquipmentIdNo,         // last 6 chars only — not full identifier
    string? EquipmentSerialNo,     // last 6 chars only
    string? EquipmentSwl,          // capacity / safe working load (non-PII)
    string? ClientName,            // company name is non-PII
    DateOnly? InspectionDate,      // last inspection (from linked certificate)
    DateOnly? ValidUntil,
    bool IsValidNow,
    string? CertificateNo,
    string? InspectorName,         // inspector full name from the linked certificate
    string? InspectorSapNo,        // inspector SAP / employee number
    DateTime? IssuedAtUtc);
