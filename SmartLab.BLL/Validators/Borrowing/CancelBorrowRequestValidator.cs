using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class CancelBorrowRequestValidator : AbstractValidator<CancelBorrowRequest>
{
    public const int ReasonMaxLength = 500;

    public CancelBorrowRequestValidator()
    {
        RuleFor(x => x.Reason)
            .MaximumLength(ReasonMaxLength).WithMessage($"Lý do tối đa {ReasonMaxLength} ký tự");
    }
}
