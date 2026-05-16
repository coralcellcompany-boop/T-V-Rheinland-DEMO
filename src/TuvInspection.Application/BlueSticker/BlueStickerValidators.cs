using FluentValidation;
using TuvInspection.Contracts.BlueSticker;

namespace TuvInspection.Application.BlueSticker;

public sealed class UpdateBlueStickerInspectionRequestValidator
    : AbstractValidator<UpdateBlueStickerInspectionRequest>
{
    public UpdateBlueStickerInspectionRequestValidator()
    {
        RuleFor(x => x.AreaOfInspection).NotEmpty().WithMessage("Area of inspection is required.");
        RuleFor(x => x.Result).NotEqual(BlueStickerResultDto.NotSet)
            .WithMessage("Inspection result must be Pass or Fail.");
        RuleFor(x => x.ReceiverName).NotEmpty().WithMessage("Receiver name is required.");
        RuleFor(x => x.ReceiverBadgeNo).NotEmpty().WithMessage("Receiver badge No. is required.");
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
