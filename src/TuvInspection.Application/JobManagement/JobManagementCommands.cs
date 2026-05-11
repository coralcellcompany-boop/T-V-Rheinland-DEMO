using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Common;
using TuvInspection.Contracts.JobManagement;

namespace TuvInspection.Application.JobManagement;

// Job Requests
public sealed record ListJobRequestsQuery(
    Guid? ClientId, JobRequestStatusDto? Status, string? Search,
    int Page, int PageSize) : IQuery<PagedResult<JobRequestListItemDto>>;
public sealed record GetJobRequestByIdQuery(Guid Id) : IQuery<JobRequestDetailDto?>;
public sealed record CreateJobRequestCommand(CreateJobRequestRequest Body) : ICommand<JobRequestDetailDto>;
public sealed record UpdateJobRequestCommand(Guid Id, UpdateJobRequestRequest Body) : ICommand<JobRequestDetailDto>;
public sealed record AcceptJobRequestCommand(Guid Id) : ICommand<JobRequestDetailDto>;
public sealed record RejectJobRequestCommand(Guid Id, RejectJobRequestRequest Body) : ICommand<JobRequestDetailDto>;
public sealed record ConvertJobRequestCommand(Guid Id) : ICommand<JobOrderDetailDto>;

// Job Orders
public sealed record ListJobOrdersQuery(
    Guid? ClientId, JobOrderStatusDto? Status, string? Search,
    string? AssignedInspectorId, bool MineOnly,
    int Page, int PageSize) : IQuery<PagedResult<JobOrderListItemDto>>;
public sealed record GetJobOrderByIdQuery(Guid Id) : IQuery<JobOrderDetailDto?>;
public sealed record CreateJobOrderCommand(CreateJobOrderRequest Body) : ICommand<JobOrderDetailDto>;
public sealed record UpdateJobOrderCommand(Guid Id, UpdateJobOrderRequest Body) : ICommand<JobOrderDetailDto>;
public sealed record BeginJobOrderCommand(Guid Id) : ICommand<JobOrderDetailDto>;
public sealed record CompleteJobOrderCommand(Guid Id) : ICommand<JobOrderDetailDto>;
public sealed record CancelJobOrderCommand(Guid Id) : ICommand<JobOrderDetailDto>;
public sealed record AutoAssignJobOrderInspectorCommand(Guid Id) : ICommand<JobOrderDetailDto>;

// DWR / Timesheets
public sealed record ListDwrQuery(
    Guid? JobOrderId, string? InspectorId, DwrStatusDto? Status,
    DateOnly? DateFrom, DateOnly? DateTo, string? Search,
    int Page, int PageSize) : IQuery<PagedResult<DwrListItemDto>>;
public sealed record GetDwrByIdQuery(Guid Id) : IQuery<DwrDetailDto?>;
public sealed record CreateDwrCommand(CreateDwrRequest Body) : ICommand<DwrDetailDto>;
public sealed record UpdateDwrCommand(Guid Id, UpdateDwrRequest Body) : ICommand<DwrDetailDto>;
public sealed record SubmitDwrCommand(Guid Id) : ICommand<DwrDetailDto>;
public sealed record ApproveDwrCommand(Guid Id) : ICommand<DwrDetailDto>;
public sealed record RejectDwrCommand(Guid Id, DwrRejectRequest Body) : ICommand<DwrDetailDto>;

// Surveys
public sealed record ListSurveysQuery(
    Guid? ClientId, SurveyStatusDto? Status, string? Search,
    int Page, int PageSize) : IQuery<PagedResult<SurveyListItemDto>>;
public sealed record GetSurveyByIdQuery(Guid Id) : IQuery<SurveyDetailDto?>;
public sealed record CreateSurveyCommand(CreateSurveyRequest Body) : ICommand<SurveyDetailDto>;
public sealed record UpdateSurveyCommand(Guid Id, UpdateSurveyRequest Body) : ICommand<SurveyDetailDto>;
public sealed record SubmitSurveyCommand(Guid Id) : ICommand<SurveyDetailDto>;
