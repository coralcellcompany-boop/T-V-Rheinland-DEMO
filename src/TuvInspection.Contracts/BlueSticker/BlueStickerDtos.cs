namespace TuvInspection.Contracts.BlueSticker;

public enum BlueStickerReportStateDto
{
    Draft = 0, InProgress = 1, UnderReview = 2, Approved = 3,
    AwaitingClientSignature = 4, ClientSigned = 5, Rejected = 6, Voided = 7
}

public enum BlueStickerResultDto { NotSet = 0, Pass = 1, Fail = 2 }

public enum BlueStickerTriggerDto
{
    StartInspection, SubmitForReview, Approve, Reject, RequestClientOtp, VerifyOtpAndSign, Void
}

/// <summary>Coordinator admin fields, supplied when a Blue Sticker job order is created.</summary>
public sealed record CreateBlueStickerReportsRequest(
    Guid JobOrderId,
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? DepartmentContractor);

/// <summary>Inspector data entry (InProgress only).</summary>
public sealed record UpdateBlueStickerInspectionRequest(
    string? AreaOfInspection,
    BlueStickerResultDto Result,
    string? Deficiencies,
    string? CorrectiveActionsTaken,
    string? EquipmentLocation,
    string? ReceiverName,
    string? ReceiverBadgeNo,
    string? ReceiverTelephone,
    string? InspectorTelephone);

public sealed record BlueStickerTransitionRequest(string? Comments, string? InspectorSignaturePng,
    string? TechnicalReviewerSignaturePng);

public sealed record RequestClientOtpRequest();   // body intentionally empty

public sealed record VerifyOtpAndSignRequest(string Otp, string ReceiverSignaturePng);

public sealed record BlueStickerTransitionDto(
    string FromState, string ToState, string ActorUserId, string ActorRole,
    string? Comments, DateTime AtUtc);

public sealed record BlueStickerReportDetailDto(
    Guid Id,
    string ReportNo,
    Guid JobOrderId,
    Guid EquipmentId,
    Guid ClientId,
    string TuvJobOrderNo,
    string? AramcoCategoryNo,
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? DepartmentContractor,
    DateOnly? InspectionDate,
    TimeOnly? InspectionTime,
    string? PreviousStickerNo,
    string? PreviousStickerIssuedBy,
    string? AreaOfInspection,
    BlueStickerResultDto Result,
    string EquipmentIdNo,
    string? Capacity,
    string? EquipmentLocation,
    string? Manufacturer,
    string? Model,
    string? EquipmentType,
    string? EquipmentSerialNo,
    string? NewStickerNo,
    DateOnly? StickerExpirationDate,
    string? Deficiencies,
    string? CorrectiveActionsTaken,
    string? ReceiverName,
    string? ReceiverBadgeNo,
    string? ReceiverTelephone,
    string? InspectorName,
    string? InspectorSapNo,
    string? InspectorTelephone,
    string? TechnicalReviewerName,
    DateOnly? ReceivedDate,
    DateOnly? ReviewedDate,
    string? ReceiverSignaturePng,
    string? InspectorSignaturePng,
    string? TechnicalReviewerSignaturePng,
    BlueStickerReportStateDto State,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<BlueStickerTransitionDto> Transitions);

public sealed record BlueStickerReportListItemDto(
    Guid Id, string ReportNo, string TuvJobOrderNo, string EquipmentIdNo,
    BlueStickerReportStateDto State, DateOnly? InspectionDate, DateTime CreatedAtUtc);
