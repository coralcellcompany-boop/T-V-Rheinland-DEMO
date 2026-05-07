using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Certificates;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Application.Certificates;

public sealed record ListCertificatesQuery(
    Guid? ClientId,
    Guid? EquipmentId,
    Guid? JobOrderId,
    CertificateStateDto? State,
    CertificateInspectionTypeDto? InspectionType,
    InspectionResultDto? Result,
    string? Search,
    /// <summary>
    /// True → only certs whose equipment carries an Aramco category (Blue Sticker line).
    /// False → only certs whose equipment is NOT Aramco-categorised (Third Party line).
    /// Null → no filter (the unified all-services view).
    /// </summary>
    bool? AramcoOnly,
    int Page,
    int PageSize) : IQuery<PagedResult<CertificateListItemDto>>;

public sealed record GetCertificateByIdQuery(Guid Id) : IQuery<CertificateDetailDto?>;

/// <summary>
/// Auxiliary query feeding the Annex 1 PDF renderer with the inspector + equipment
/// context not present on the cert DTO (inspector full name, SAP No., equipment SWL).
/// </summary>
public sealed record CertificateInspectorContext(
    string? InspectorName,
    string? InspectorSapNo,
    string? EquipmentSwl);

public sealed record GetCertificateInspectorContextQuery(Guid CertificateId)
    : IQuery<CertificateInspectorContext>;

public sealed record GetApprovalQueueCountsQuery() : IQuery<ApprovalQueueCountsDto>;

public sealed record ListApprovalQueueQuery(
    string Bucket,           // "pending" | "rejected" | "mine"
    int Page,
    int PageSize) : IQuery<PagedResult<CertificateListItemDto>>;

public sealed record CreateCertificateCommand(CreateCertificateRequest Body) : ICommand<CertificateDetailDto>;

public sealed record UpdateCertificateCommand(Guid Id, UpdateCertificateRequest Body) : ICommand<CertificateDetailDto>;

public sealed record FireCertificateTriggerCommand(
    Guid Id,
    CertificateTriggerDto Trigger,
    string? Comments) : ICommand<CertificateDetailDto>;
