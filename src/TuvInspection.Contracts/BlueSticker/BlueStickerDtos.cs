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

/// <summary>Coordinator-supplied payload for batch-creating Blue Sticker reports under a job
/// order. When <c>EquipmentIds</c> is omitted or empty, ALL Aramco-categorised equipment under
/// the job order's client gets a report (legacy behaviour). When non-empty, only the listed
/// equipment IDs do — and they must each belong to that client and be Aramco-categorised.</summary>
public sealed record CreateBlueStickerReportsRequest(
    Guid JobOrderId,
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? DepartmentContractor,
    IReadOnlyList<Guid>? EquipmentIds);

/// <summary>Coordinator admin fields update (Draft only).</summary>
public sealed record UpdateBlueStickerAdminRequest(
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? DepartmentContractor,
    string? AramcoCategoryNo,
    string? PreviousStickerNo,
    string? PreviousStickerIssuedBy);

/// <summary>Inspector data entry (InProgress only). Includes the equipment snapshot fields
/// the inspector confirms / corrects on site (Aramco Category, Mfr, Model, Type, Serial,
/// Capacity) — the Coordinator pre-fills these from the Equipment catalog at Create time but
/// the inspector has the final say while the report is InProgress.</summary>
public sealed record UpdateBlueStickerInspectionRequest(
    string? AreaOfInspection,
    BlueStickerResultDto Result,
    string? Deficiencies,
    string? CorrectiveActionsTaken,
    string? EquipmentLocation,
    string? ReceiverName,
    string? ReceiverBadgeNo,
    string? ReceiverTelephone,
    string? InspectorTelephone,
    string? AramcoCategoryNo,
    string? Manufacturer,
    string? Model,
    string? EquipmentType,
    string? EquipmentSerialNo,
    string? Capacity);

public sealed record BlueStickerTransitionRequest(string? Comments, string? InspectorSignaturePng,
    string? TechnicalReviewerSignaturePng);

public sealed record RequestClientOtpRequest();   // body intentionally empty

/// <summary>Response of POST /request-otp. <c>DevOtp</c> is populated only in Development
/// environment to ease manual testing without checking MailHog. Always null in production.</summary>
public sealed record RequestClientOtpResponse(
    BlueStickerReportDetailDto Report,
    string? DevOtp);

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
    IReadOnlyList<BlueStickerTransitionDto> Transitions,
    /// <summary>SAIC-U-70## inspection checklist number derived from the equipment's Aramco
    /// category — surfaced so the inspector knows which checklist applies.</summary>
    string? InspectionChecklistNumber);

public sealed record BlueStickerReportListItemDto(
    Guid Id, string ReportNo, string TuvJobOrderNo, string EquipmentIdNo,
    BlueStickerReportStateDto State, DateOnly? InspectionDate, DateTime CreatedAtUtc);
