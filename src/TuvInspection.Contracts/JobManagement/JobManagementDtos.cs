namespace TuvInspection.Contracts.JobManagement;

// ---------- Job Requests ----------
public enum JobRequestStatusDto
{ New = 0, UnderReview = 1, Accepted = 2, Rejected = 3, Converted = 4 }

public sealed record JobRequestListItemDto(
    Guid Id, string RequestNo, Guid ClientId, string ClientName,
    int Service, DateOnly RequestedFrom, DateOnly RequestedTo,
    string? Site, string? ContactEmail,
    JobRequestStatusDto Status, Guid? ConvertedJobOrderId,
    DateTime CreatedAtUtc);

public sealed record JobRequestDetailDto(
    Guid Id, string RequestNo, Guid ClientId, string ClientName,
    int Service, DateOnly RequestedFrom, DateOnly RequestedTo,
    string? Site, string? ContactName, string? ContactPhone, string? ContactEmail,
    string? ScopeNotes, string? PoReference,
    JobRequestStatusDto Status, Guid? ConvertedJobOrderId, string? RejectionReason,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record CreateJobRequestRequest(
    Guid ClientId, int Service, DateOnly RequestedFrom, DateOnly RequestedTo,
    string? Site, string? ContactName, string? ContactPhone, string? ContactEmail,
    string? ScopeNotes, string? PoReference);

public sealed record UpdateJobRequestRequest(
    DateOnly RequestedFrom, DateOnly RequestedTo,
    string? Site, string? ContactName, string? ContactPhone, string? ContactEmail,
    string? ScopeNotes, string? PoReference);

public sealed record RejectJobRequestRequest(string Reason);

// ---------- Job Orders ----------
public enum JobOrderStatusDto { Open = 0, InProgress = 1, Completed = 2, Cancelled = 3 }
public enum ServiceTypeDto { None = 0, ThirdPartyInspection = 1, BlueSticker = 2, OperatorAssessment = 4, All = 7 }

public sealed record JobOrderListItemDto(
    Guid Id, string JobOrderNo, Guid ClientId, string ClientName,
    ServiceTypeDto Service, DateOnly DateFrom, DateOnly DateTo,
    string? Location, JobOrderStatusDto Status,
    int AssignedInspectorCount,
    int AttachmentCount,
    DateTime CreatedAtUtc);

public sealed record JobOrderDetailDto(
    Guid Id, string JobOrderNo, Guid ClientId, string ClientName,
    ServiceTypeDto Service, DateOnly DateFrom, DateOnly DateTo,
    string? Location, JobOrderStatusDto Status,
    IReadOnlyList<string> AssignedInspectorIds,
    IReadOnlyList<string> AttachmentKeys,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

/// <summary>
/// Creates one or more Job Orders. <see cref="Quantity"/> &gt; 1 spawns that many
/// independent Job Orders (each with its own auto-generated JobOrderNo) sharing the
/// same client/service/dates/location/attachments — per Ahmed comment #1.
/// </summary>
public sealed record CreateJobOrderRequest(
    Guid ClientId, ServiceTypeDto Service,
    DateOnly DateFrom, DateOnly DateTo, string? Location,
    int Quantity = 1,
    IReadOnlyList<string>? AttachmentKeys = null);

public sealed record UpdateJobOrderRequest(
    DateOnly DateFrom, DateOnly DateTo, string? Location,
    JobOrderStatusDto Status, IReadOnlyList<string> AssignedInspectorIds,
    IReadOnlyList<string>? AttachmentKeys = null);

// ---------- Daily Work Reports ----------
public enum DwrStatusDto { Draft = 0, Submitted = 1, Approved = 2, Rejected = 3 }

public sealed record DwrListItemDto(
    Guid Id, string DwrNo, Guid JobOrderId, string JobOrderNo,
    Guid ClientId, string ClientName,
    string InspectorId, string? InspectorName,
    DateOnly Date, TimeOnly TimeFrom, TimeOnly TimeTo,
    int EquipmentInspected, int OperatorsAssessed,
    DwrStatusDto Status, DateTime CreatedAtUtc);

public sealed record DwrDetailDto(
    Guid Id, string DwrNo, Guid JobOrderId, string JobOrderNo,
    Guid ClientId, string ClientName,
    string InspectorId, string? InspectorName,
    DateOnly Date, TimeOnly TimeFrom, TimeOnly TimeTo,
    string? Location, int EquipmentInspected, int OperatorsAssessed,
    string? Notes, DwrStatusDto Status, string? RejectionReason,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record CreateDwrRequest(
    Guid JobOrderId, DateOnly Date, TimeOnly TimeFrom, TimeOnly TimeTo,
    string? Location, int EquipmentInspected, int OperatorsAssessed, string? Notes);

public sealed record UpdateDwrRequest(
    DateOnly Date, TimeOnly TimeFrom, TimeOnly TimeTo,
    string? Location, int EquipmentInspected, int OperatorsAssessed, string? Notes);

public sealed record DwrRejectRequest(string Reason);

// ---------- Surveys ----------
public enum SurveyStatusDto { Draft = 0, Submitted = 1, ConvertedToJobOrder = 2 }

public sealed record SurveyListItemDto(
    Guid Id, string SurveyNo, Guid ClientId, string ClientName,
    DateOnly Date, string? Site, int EstimatedEquipmentCount,
    SurveyStatusDto Status, Guid? ConvertedJobOrderId, DateTime CreatedAtUtc);

public sealed record SurveyDetailDto(
    Guid Id, string SurveyNo, Guid ClientId, string ClientName,
    DateOnly Date, string? Site, string? GpsLatLng,
    int EstimatedEquipmentCount, string? AccessNotes, string? SafetyNotes,
    string? Recommendation, string? SurveyorUserId,
    SurveyStatusDto Status, Guid? ConvertedJobOrderId,
    DateTime CreatedAtUtc, DateTime? UpdatedAtUtc);

public sealed record CreateSurveyRequest(Guid ClientId, DateOnly Date, string? Site);

public sealed record UpdateSurveyRequest(
    DateOnly Date, string? Site, string? GpsLatLng,
    int EstimatedEquipmentCount, string? AccessNotes, string? SafetyNotes,
    string? Recommendation, string? SurveyorUserId);
