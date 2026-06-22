using FluentValidation;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Application.BlueSticker;

public sealed class UpdateBlueStickerInspectionRequestValidator
    : AbstractValidator<UpdateBlueStickerInspectionRequest>
{
    public UpdateBlueStickerInspectionRequestValidator()
    {
        // Save accepts partial drafts — required-field enforcement happens at SubmitForReview
        // (see FireBlueStickerTriggerHandler). These rules only guard length / format so users
        // can save mid-inspection without losing work.
        RuleFor(x => x.AreaOfInspection).MaximumLength(300);
        RuleFor(x => x.Deficiencies).MaximumLength(4000);
        RuleFor(x => x.CorrectiveActionsTaken).MaximumLength(4000);
        RuleFor(x => x.ReceiverName).MaximumLength(200);
        RuleFor(x => x.ReceiverBadgeNo).MaximumLength(50);
        RuleFor(x => x.ReceiverTelephone).MaximumLength(40);
        RuleFor(x => x.InspectorTelephone).MaximumLength(40);
        RuleFor(x => x.EquipmentLocation).MaximumLength(200);
        RuleFor(x => x.AramcoCategoryNo).MaximumLength(20);
        RuleFor(x => x.Manufacturer).MaximumLength(200);
        RuleFor(x => x.Model).MaximumLength(200);
        RuleFor(x => x.EquipmentType).MaximumLength(200);
        RuleFor(x => x.EquipmentSerialNo).MaximumLength(100);
        RuleFor(x => x.Capacity).MaximumLength(100);
    }
}

public sealed class VerifyOtpAndSignRequestValidator : AbstractValidator<VerifyOtpAndSignRequest>
{
    public VerifyOtpAndSignRequestValidator()
    {
        RuleFor(x => x.Otp).NotEmpty().Matches(@"^\d{6}$").WithMessage("OTP must be 6 digits.");
        RuleFor(x => x.ReceiverSignaturePng).NotEmpty()
            .Must(s => s!.StartsWith("data:image/")).WithMessage("A client signature is required.");
    }
}
