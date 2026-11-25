using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ReturnSlipLineRequestValidator : AbstractValidator<ReturnSlipLineRequest>
{
    public ReturnSlipLineRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Món trả là bắt buộc");

        RuleFor(x => x.Quantity)
            .GreaterThanOrEqualTo(0).When(x => x.Quantity.HasValue)
            .WithMessage("Số lượng trả không được âm");
    }
}
