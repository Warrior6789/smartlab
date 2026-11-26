using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class CancelReplacementRequestValidator : AbstractValidator<CancelReplacementRequest>
{
    public CancelReplacementRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Phải chọn dòng món thay thế cần hủy");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Phải ghi lý do hủy để gửi cho sinh viên")
            .MaximumLength(CancelBorrowRequestValidator.ReasonMaxLength)
            .WithMessage($"Lý do tối đa {CancelBorrowRequestValidator.ReasonMaxLength} ký tự");
    }
}
