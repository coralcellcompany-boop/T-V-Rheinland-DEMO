using FluentValidation;
using TuvInspection.Contracts.Users;

namespace TuvInspection.Application.Users;

public sealed class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(200);
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SapNo).MaximumLength(50);
        RuleFor(x => x.CertNo).MaximumLength(50);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(12)
            .WithMessage("Password must be at least 12 characters.");
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role is required.");
    }
}

public sealed class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.FullName).NotEmpty().MaximumLength(200);
        RuleFor(x => x.SapNo).MaximumLength(50);
        RuleFor(x => x.CertNo).MaximumLength(50);
        RuleFor(x => x.Roles).NotEmpty().WithMessage("At least one role is required.");
    }
}

public sealed class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleFor(x => x.NewPassword).NotEmpty().MinimumLength(12)
            .WithMessage("Password must be at least 12 characters.");
    }
}
