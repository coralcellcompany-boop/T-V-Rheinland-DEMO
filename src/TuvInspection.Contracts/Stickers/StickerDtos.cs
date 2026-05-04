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
    Guid? ClientId,
    string? ClientName,
    Guid? IssuedToCertificateId,
    string? CertificateNo,
    Guid? IssuedToEquipmentId,
    string? EquipmentIdNo,
    DateOnly? ValidUntil,
    DateTime CreatedAtUtc);

public sealed record StickerStockSummaryDto(
    int Unallocated,
    int Issued,
    int Voided,
    int Expired);

public sealed record ProcureStockRequest(int Count);

public sealed record VoidStickerRequest(string Reason);

/// <summary>Public verification payload — deliberately excludes PII.</summary>
public sealed record StickerPublicViewDto(
    string StickerNo,
    StickerStateDto State,
    string? AramcoCategory,
    string? EquipmentTypeName,
    string? EquipmentIdNo,         // last 6 chars only — not full identifier
    string? ClientName,            // company name is non-PII
    DateOnly? ValidUntil,
    bool IsValidNow,
    string? CertificateNo,
    DateTime? IssuedAtUtc);
