namespace TuvInspection.Contracts.Assessments;

public enum CompetencyCategoryDto
{
    None = 0,
    MobileCrane = 1,
    Forklift = 2,
    Manlift = 3,
    WheelLoader = 4,
    MewpTelehandler = 5
}

public enum AssessmentStateDto
{
    Draft = 0,
    Submitted = 1,
    Approved = 2,
    Rejected = 3,
    Expired = 4
}

public enum AssessmentResultDto { NotSet = 0, Pass = 1, Fail = 2 }

public enum AssessmentTriggerDto { Submit, Approve, Reject, Resubmit, Expire }

public enum CompetencyCardStateDto { Issued = 0, Lost = 1, Suspended = 2, Expired = 3, Revoked = 4 }

// ---------- Candidates ----------

public sealed record CandidateListItemDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string FullName,
    string IdentificationNumber,
    string? Phone,
    string? Email,
    string? Nationality,
    bool IsActive,
    DateTime CreatedAtUtc);

public sealed record CandidateDetailDto(
    Guid Id,
    Guid ClientId,
    string ClientName,
    string FullName,
    string IdentificationNumber,
    string? Phone,
    string? Email,
    string? EmployeeNo,
    string? Nationality,
    DateOnly? DateOfBirth,
    string? PhotoKey,
    bool IsActive,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc);

public sealed record CreateCandidateRequest(
    Guid ClientId,
    string FullName,
    string IdentificationNumber,
    string? Phone,
    string? Email,
    string? EmployeeNo,
    string? Nationality,
    DateOnly? DateOfBirth);

public sealed record UpdateCandidateRequest(
    string FullName,
    string IdentificationNumber,
    string? Phone,
    string? Email,
    string? EmployeeNo,
    string? Nationality,
    DateOnly? DateOfBirth,
    bool IsActive);

// ---------- Assessments ----------

public sealed record AssessmentTransitionDto(
    Guid Id,
    AssessmentStateDto FromState,
    AssessmentStateDto ToState,
    string ActorUserId,
    string ActorRole,
    string? Comments,
    DateTime AtUtc);

public sealed record AssessmentListItemDto(
    Guid Id,
    string AssessmentNo,
    Guid CandidateId,
    string CandidateName,
    Guid ClientId,
    string ClientName,
    CompetencyCategoryDto Category,
    DateOnly AssessmentDate,
    DateOnly? NextAssessmentDate,
    AssessmentResultDto Result,
    AssessmentStateDto State,
    string? IssuedCardNo,
    DateTime CreatedAtUtc);

public sealed record AssessmentDetailDto(
    Guid Id,
    string AssessmentNo,
    Guid CandidateId,
    string CandidateName,
    string CandidateIdNumber,
    Guid ClientId,
    string ClientName,
    CompetencyCategoryDto Category,
    DateOnly AssessmentDate,
    DateOnly? NextAssessmentDate,
    string? Location,
    int? TheoreticalScore,
    int? PracticalScore,
    AssessmentResultDto Result,
    string? Comments,
    AssessmentStateDto State,
    Guid? IssuedCardId,
    string? IssuedCardNo,
    DateTime CreatedAtUtc,
    DateTime? UpdatedAtUtc,
    IReadOnlyList<AssessmentTransitionDto> Transitions);

public sealed record CreateAssessmentRequest(
    Guid CandidateId,
    CompetencyCategoryDto Category,
    DateOnly AssessmentDate,
    string? Location);

public sealed record UpdateAssessmentRequest(
    DateOnly AssessmentDate,
    DateOnly? NextAssessmentDate,
    string? Location,
    int? TheoreticalScore,
    int? PracticalScore,
    AssessmentResultDto Result,
    string? Comments);

public sealed record AssessmentTransitionRequest(string? Comments);

// ---------- Cards ----------

public sealed record CompetencyCardListItemDto(
    Guid Id,
    string CardNo,
    Guid AssessmentId,
    string AssessmentNo,
    Guid CandidateId,
    string CandidateName,
    Guid ClientId,
    string ClientName,
    CompetencyCategoryDto Category,
    DateOnly IssuedOn,
    DateOnly? ValidUntil,
    CompetencyCardStateDto State);

public sealed record CompetencyCardPublicViewDto(
    string CardNo,
    CompetencyCategoryDto Category,
    string CandidateNameMasked,            // first name + last initial
    string CandidateIdMasked,              // last 4 digits
    string? ClientName,
    DateOnly IssuedOn,
    DateOnly? ValidUntil,
    bool IsValidNow,
    CompetencyCardStateDto State);

public sealed record CompetencyCardDetailDto(
    Guid Id,
    string CardNo,
    Guid AssessmentId,
    string AssessmentNo,
    Guid CandidateId,
    string CandidateName,
    string CandidateIdNumber,
    string? CandidateNationality,
    Guid ClientId,
    string ClientName,
    CompetencyCategoryDto Category,
    DateOnly IssuedOn,
    DateOnly? ValidUntil,
    CompetencyCardStateDto State,
    string? StatusReason);
