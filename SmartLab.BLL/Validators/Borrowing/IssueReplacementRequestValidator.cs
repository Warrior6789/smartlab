using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class IssueReplacementRequestValidator : AbstractValidator<IssueReplacementRequest>
{
    public IssueReplacementRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Phải chọn dòng món thay thế cần giao");

        RuleFor(x => x.ItemId)
            .NotEmpty().WithMessage("Phải quét con thay thế đem giao");

        RuleFor(x => x.PhotoId)
            .NotEmpty().WithMessage("Phải chụp ảnh giao đồ (linh kiện kèm thẻ sinh viên) trước khi giao");

        RuleFor(x => x.OverrideReason)
            .MaximumLength(IssueBorrowRequestValidator.OverrideReasonMaxLength)
            .WithMessage($"Lý do tối đa {IssueBorrowRequestValidator.OverrideReasonMaxLength} ký tự");
    }
}
