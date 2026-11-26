using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class IssueBorrowRequestValidator : AbstractValidator<IssueBorrowRequest>
{
    public const int OverrideReasonMaxLength = 500;

    public IssueBorrowRequestValidator()
    {
        RuleFor(x => x.PhotoId)
            .NotEmpty().WithMessage("Phải chụp ảnh giao đồ (linh kiện kèm thẻ sinh viên) trước khi giao");

        RuleFor(x => x.OverrideReason)
            .MaximumLength(OverrideReasonMaxLength).WithMessage($"Lý do tối đa {OverrideReasonMaxLength} ký tự");

        RuleFor(x => x.Units)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách con giao không được null")
            .Must(units => units.Where(u => u != null).GroupBy(u => u.BorrowItemId).All(g => g.Count() == 1))
            .WithMessage("Mỗi dòng linh kiện chỉ được xuất hiện 1 lần")
            .Must(units => units.Where(u => u?.ItemIds != null).SelectMany(u => u.ItemIds).GroupBy(id => id).All(g => g.Count() == 1))
            .WithMessage("Một con chỉ được chọn 1 lần");

        RuleForEach(x => x.Units)
            .NotNull().WithMessage("Dòng không được null")
            .SetValidator(new IssueUnitRequestValidator());
    }
}
