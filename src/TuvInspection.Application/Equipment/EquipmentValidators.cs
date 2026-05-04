using FluentValidation;
using TuvInspection.Contracts.Equipment;

namespace TuvInspection.Application.Equipment;

public sealed class CreateEquipmentRequestValidator : AbstractValidator<CreateEquipmentRequest>
{
    public CreateEquipmentRequestValidator()
    {
        RuleFor(x => x.ClientId).NotEmpty();
        RuleFor(x => x.EquipmentTypeId).NotEmpty();
        RuleFor(x => x.IdNo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SerialNo).MaximumLength(150);
        RuleFor(x => x.Manufacturer).MaximumLength(150);
        RuleFor(x => x.Model).MaximumLength(150);
        RuleFor(x => x.YearOfManufacture)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 1)
            .When(x => x.YearOfManufacture is not null);
        RuleFor(x => x.Swl).MaximumLength(50);
        RuleFor(x => x.Location).MaximumLength(300);
    }
}

public sealed class UpdateEquipmentRequestValidator : AbstractValidator<UpdateEquipmentRequest>
{
    public UpdateEquipmentRequestValidator()
    {
        RuleFor(x => x.EquipmentTypeId).NotEmpty();
        RuleFor(x => x.IdNo).NotEmpty().MaximumLength(100);
        RuleFor(x => x.SerialNo).MaximumLength(150);
        RuleFor(x => x.Manufacturer).MaximumLength(150);
        RuleFor(x => x.Model).MaximumLength(150);
        RuleFor(x => x.YearOfManufacture)
            .InclusiveBetween(1900, DateTime.UtcNow.Year + 1)
            .When(x => x.YearOfManufacture is not null);
        RuleFor(x => x.Swl).MaximumLength(50);
        RuleFor(x => x.Location).MaximumLength(300);
    }
}
