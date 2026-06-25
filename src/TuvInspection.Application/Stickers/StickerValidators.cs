using FluentValidation;
using TuvInspection.Contracts.Stickers;

namespace TuvInspection.Application.Stickers;

public sealed class ProcureStockRequestValidator : AbstractValidator<ProcureStockRequest>
{
    public ProcureStockRequestValidator()
    {
        RuleFor(x => x.Color).IsInEnum();
        RuleFor(x => x.Count).InclusiveBetween(1, 1000);
    }
}

public sealed class AssignStickersRequestValidator : AbstractValidator<AssignStickersRequest>
{
    public AssignStickersRequestValidator()
    {
        RuleFor(x => x.InspectorUserId).NotEmpty();
        RuleFor(x => x.Color).IsInEnum();
        RuleFor(x => x.Count).InclusiveBetween(1, 500);
    }
}

public sealed class VoidStickerRequestValidator : AbstractValidator<VoidStickerRequest>
{
    public VoidStickerRequestValidator()
    {
        RuleFor(x => x.Reason).NotEmpty().MaximumLength(500);
    }
}

