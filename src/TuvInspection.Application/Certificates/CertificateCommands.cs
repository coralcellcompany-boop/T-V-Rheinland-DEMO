using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Certificates;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Application.Certificates;

public sealed record ListCertificatesQuery(
    Guid? ClientId,
    Guid? EquipmentId,
    CertificateStateDto? State,
    CertificateInspectionTypeDto? InspectionType,
    InspectionResultDto? Result,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<CertificateListItemDto>>;

public sealed record GetCertificateByIdQuery(Guid Id) : IQuery<CertificateDetailDto?>;

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
