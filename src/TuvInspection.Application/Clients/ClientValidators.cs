using FluentValidation;
using TuvInspection.Contracts.Clients;

namespace TuvInspection.Application.Clients;

public sealed class CreateClientRequestValidator : AbstractValidator<CreateClientRequest>
{
    public CreateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Code).NotEmpty().MaximumLength(40)
            .Matches("^[A-Z0-9._-]+$").WithMessage("Code must be uppercase letters, digits, dot, dash or underscore.");
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactPhone).MaximumLength(50);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}

public sealed class UpdateClientRequestValidator : AbstractValidator<UpdateClientRequest>
{
    public UpdateClientRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Address).MaximumLength(500);
        RuleFor(x => x.ContactName).MaximumLength(200);
        RuleFor(x => x.ContactPhone).MaximumLength(50);
        RuleFor(x => x.ContactEmail).EmailAddress().When(x => !string.IsNullOrWhiteSpace(x.ContactEmail));
    }
}
