using FluentValidation;
using TuvInspection.Contracts.Assessments;

namespace TuvInspection.Application.Assessments;

public sealed class CreateCandidateRequestValidator : AbstractValidator<CreateCandidateRequest>
{
    public CreateCandidateRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentificationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
        RuleFor(x => x.Phone).MaximumLength(50);
        RuleFor(x => x.Nationality).MaximumLength(80);
    }
}

public sealed class UpdateCandidateRequestValidator : AbstractValidator<UpdateCandidateRequest>
{
    public UpdateCandidateRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.IdentificationNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.Email).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public sealed class CreateAssessmentRequestValidator : AbstractValidator<CreateAssessmentRequest>
{
    public CreateAssessmentRequestValidator()
    {
        RuleFor(x => x.CandidateId).NotEmpty();
        RuleFor(x => x.Category).IsInEnum().NotEqual(CompetencyCategoryDto.None);
        RuleFor(x => x.AssessmentDate).NotEmpty();
        RuleFor(x => x.Location).MaximumLength(300);
    }
}

public sealed class UpdateAssessmentRequestValidator : AbstractValidator<UpdateAssessmentRequest>
{
    public UpdateAssessmentRequestValidator()
    {
        RuleFor(x => x.AssessmentDate).NotEmpty();
        RuleFor(x => x.TheoreticalScore).InclusiveBetween(0, 100).When(x => x.TheoreticalScore is not null);
        RuleFor(x => x.PracticalScore).InclusiveBetween(0, 100).When(x => x.PracticalScore is not null);
        RuleFor(x => x.Comments).MaximumLength(2000);
        RuleFor(x => x.Location).MaximumLength(300);
    }
}
