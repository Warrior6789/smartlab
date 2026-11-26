using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ReplaceDamagedRequestValidator : AbstractValidator<ReplaceDamagedRequest>
{
    public ReplaceDamagedRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Phải chọn con bị hỏng cần đổi");

        RuleFor(x => x.PhotoId)
            .NotEmpty().WithMessage("Phải chụp ảnh con bị hỏng trước khi đổi");
    }
}
