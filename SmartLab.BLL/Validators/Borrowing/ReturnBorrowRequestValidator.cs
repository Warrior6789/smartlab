using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ReturnBorrowRequestValidator : AbstractValidator<ReturnBorrowRequest>
{
    public ReturnBorrowRequestValidator()
    {
        RuleFor(x => x.PhotoId)
            .NotEmpty().WithMessage("Phải chụp ảnh đồ trả về trước khi nhận trả");

        RuleFor(x => x.Lines)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phải chọn ít nhất 1 dòng linh kiện trả về")
            .Must(lines => lines.Where(l => l != null).GroupBy(l => l.BorrowItemId).All(g => g.Count() == 1))
            .WithMessage("Mỗi dòng linh kiện chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.Lines)
            .NotNull().WithMessage("Dòng không được null")
            .SetValidator(new ReturnLineRequestValidator());
    }
}
