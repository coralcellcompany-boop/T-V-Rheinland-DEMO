using TuvInspection.Application.Common.Cqrs;
using TuvInspection.Contracts.Assessments;
using TuvInspection.Contracts.Common;

namespace TuvInspection.Application.Assessments;

// Candidates
public sealed record ListCandidatesQuery(
    Guid? ClientId,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<CandidateListItemDto>>;

public sealed record GetCandidateByIdQuery(Guid Id) : IQuery<CandidateDetailDto?>;

public sealed record CreateCandidateCommand(CreateCandidateRequest Body) : ICommand<CandidateDetailDto>;

public sealed record UpdateCandidateCommand(Guid Id, UpdateCandidateRequest Body) : ICommand<CandidateDetailDto>;

// Assessments
public sealed record ListAssessmentsQuery(
    Guid? CandidateId,
    Guid? ClientId,
    AssessmentStateDto? State,
    CompetencyCategoryDto? Category,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<AssessmentListItemDto>>;

public sealed record GetAssessmentByIdQuery(Guid Id) : IQuery<AssessmentDetailDto?>;

public sealed record CreateAssessmentCommand(CreateAssessmentRequest Body) : ICommand<AssessmentDetailDto>;

public sealed record UpdateAssessmentCommand(Guid Id, UpdateAssessmentRequest Body) : ICommand<AssessmentDetailDto>;

public sealed record FireAssessmentTriggerCommand(
    Guid Id,
    AssessmentTriggerDto Trigger,
    string? Comments) : ICommand<AssessmentDetailDto>;

// Cards
public sealed record ListCompetencyCardsQuery(
    Guid? ClientId,
    Guid? CandidateId,
    CompetencyCardStateDto? State,
    string? Search,
    int Page,
    int PageSize) : IQuery<PagedResult<CompetencyCardListItemDto>>;

public sealed record GetCompetencyCardPublicViewQuery(string CardNo) : IQuery<CompetencyCardPublicViewDto?>;
