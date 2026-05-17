namespace TuvInspection.Contracts.Certificates;

public enum CertificateStateDto
{
    Draft = 0,
    Submitted = 1,
    UnderReview = 2,
    AwaitingApproval = 3,
    Approved = 4,
    ClientSent = 5,
    ClientAccepted = 6,
    ClientRejected = 7,
    Rejected = 8,
    Voided = 9,
    Expired = 10,
    Archived = 11
}

public enum CertificateInspectionTypeDto { PeriodicInspection = 0, ReInspection = 1, InitialInspection = 2 }
public enum LoadTestKindDto { None = 0, Mechanical = 1, Witnessed = 2, Performed = 3 }
public enum InspectionResultDto { NotSet = 0, Pass = 1, Fail = 2, FailWithObservations = 3 }
public enum CertificateTriggerDto
{
    Submit, BeginReview, AdvanceForApproval, FinalApprove, Reject,
    Void, SendToClient, ClientAccept, ClientReject, Archive, Expire
}

public sealed record CertificateListItemDto(
    Guid Id,
    string CertificateNo,
    Guid ClientId,
    string ClientName,
    Guid EquipmentId,
    string EquipmentIdNo,
    string EquipmentTypeName,
    DateOnly InspectionDate,
    DateOnly? NextDueDate,
    CertificateInspectionTypeDto InspectionType,
    LoadTestKindDto LoadTest,
    InspectionResultDto Result,
    CertificateStateDto State,
    string? StickerNo,
    DateTime CreatedAtUtc);

public sealed record CertificateTransitionDto(
    Guid Id,
    CertificateStateDto FromState,
    CertificateStateDto ToState,
    string ActorUserId,
    string ActorRole,
    string? Comments,
    DateTime AtUtc);

public sealed record CertificateDetailDto(
    Guid Id,
    string CertificateNo,
    Guid ClientId,
    string ClientName,
    Guid EquipmentId,
    string EquipmentIdNo,
    Guid EquipmentTypeId,
    string EquipmentTypeName,
    Guid? JobOrderId,
    DateOnly InspectionDate,
    DateOnly ReportIssueDate,
    DateOnly? NextDueDate,
    CertificateInspectionTypeDto InspectionType,
    LoadTestKindDto LoadTest,
    InspectionResultDto Result,
    CertificateStateDto State,
    string? Standards,
    string? StickerNo,
    string? ChecklistJson,
    string? FindingsJson,
    string? PhotosJson,
    string? SignaturesJson,
    string? AramcoReportJson,
    string? EquipmentAramcoCategory,
    bool IsBlueStickerCertificate,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<CertificateTransitionDto> Transitions);

/// <summary>
/// One row of the Annex 1 deficiencies/corrective actions table. Persisted inside
/// <see cref="AramcoReportData.DeficiencyItems"/>. Severity follows the SAIC
/// classification (Minor/Major/Critical) so the report renderer can colour-code rows.
/// </summary>
public sealed record DeficiencyItem(
    string? Code,
    string? Description,
    DeficiencySeverity Severity,
    string? CorrectiveAction,
    DateOnly? Deadline,
    bool Resolved);

public enum DeficiencySeverity
{
    Minor = 0,
    Major = 1,
    Critical = 2,
}

/// <summary>
/// Aramco-specific Annex 1 inspection report fields. Persisted as JSON in
/// <c>InspectionCertificate.AramcoReportJson</c> and unfolded by the report renderer.
/// </summary>
public sealed record AramcoReportData(
    string? TuvJobOrderNo,
    string? AramcoCategoryNo,
    string? OrgCode,
    string? RpoNo,
    string? CrmNo,
    string? ReportNo,
    string? DepartmentContractor,
    TimeOnly? InspectionTime,
    string? PreviousStickerNo,
    string? PreviousStickerIssuedBy,
    /// <summary>Optional FK to the previously-issued <c>Sticker</c> row, when the
    /// inspector picks it from the dropdown instead of typing the number free-text.
    /// Lets the report renderer cross-link the equipment's sticker history.</summary>
    Guid? PreviousStickerId,
    string? AreaOfInspection,
    string? Capacity,
    string? EquipmentLocationOnSite,
    string? Manufacturer,
    string? Model,
    string? EquipmentType,
    string? EquipmentSerialNo,
    DateOnly? StickerExpirationDate,
    string? ReceiverName,
    string? ReceiverBadgeNo,
    string? ReceiverTelephone,
    string? InspectorTelephone,
    DateOnly? ReceivedDate,
    DateOnly? ReviewedDate,
    string? Deficiencies,
    string? CorrectiveActionsTaken,
    /// <summary>Structured deficiency rows. Preferred over the free-text
    /// <c>Deficiencies</c>/<c>CorrectiveActionsTaken</c> fields, which remain for
    /// backwards compatibility with older drafts.</summary>
    IReadOnlyList<DeficiencyItem>? DeficiencyItems);

public sealed record CreateCertificateRequest(
    Guid EquipmentId,
    Guid? JobOrderId,
    DateOnly InspectionDate,
    DateOnly ReportIssueDate,
    CertificateInspectionTypeDto InspectionType,
    string? Standards);

public sealed record UpdateCertificateRequest(
    DateOnly InspectionDate,
    DateOnly ReportIssueDate,
    DateOnly? NextDueDate,
    CertificateInspectionTypeDto InspectionType,
    LoadTestKindDto LoadTest,
    InspectionResultDto Result,
    string? Standards,
    string? StickerNo,
    string? ChecklistJson,
    string? FindingsJson,
    string? PhotosJson,
    string? SignaturesJson,
    string? AramcoReportJson);

public sealed record TransitionRequest(string? Comments);

public sealed record ApprovalQueueCountsDto(int Pending, int Rejected, int Mine);

public sealed record DashboardKpisDto(
    int TotalCertificates,
    int CertificatesThisMonth,
    int Pending,
    int Rejected,
    int DueSoon,
    int Expired,
    int ActiveEquipment,
    int Clients);
