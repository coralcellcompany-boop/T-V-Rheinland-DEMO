using FluentValidation;
using TuvInspection.Contracts.Certificates;

namespace TuvInspection.Application.Certificates;

public sealed class CreateCertificateRequestValidator : AbstractValidator<CreateCertificateRequest>
{
    public CreateCertificateRequestValidator()
    {
        RuleFor(x => x.EquipmentId).NotEmpty();
        RuleFor(x => x.InspectionDate).NotEmpty();
        RuleFor(x => x.ReportIssueDate).NotEmpty()
            .GreaterThanOrEqualTo(x => x.InspectionDate)
            .WithMessage("Report issue date must be on or after the inspection date.");
        RuleFor(x => x.Standards).MaximumLength(500);
    }
}

public sealed class UpdateCertificateRequestValidator : AbstractValidator<UpdateCertificateRequest>
{
    public UpdateCertificateRequestValidator()
    {
        RuleFor(x => x.InspectionDate).NotEmpty();
        RuleFor(x => x.ReportIssueDate).NotEmpty()
            .GreaterThanOrEqualTo(x => x.InspectionDate);
        RuleFor(x => x.Standards).MaximumLength(500);
        RuleFor(x => x.StickerNo).MaximumLength(50);
    }
}
