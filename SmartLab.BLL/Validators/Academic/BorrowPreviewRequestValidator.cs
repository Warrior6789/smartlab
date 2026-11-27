using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class BorrowPreviewRequestValidator : AbstractValidator<BorrowPreviewRequest>
{
    public const int MaxLines = 100;

    public BorrowPreviewRequestValidator()
    {
        RuleFor(x => x.Components)
            .NotNull().WithMessage("Danh sách linh kiện không hợp lệ")
            .Must(lines => lines.Count <= MaxLines).WithMessage($"Tối đa {MaxLines} linh kiện");

        RuleForEach(x => x.Components)
            .SetValidator(new ProjectComponentRequestValidator());
    }
}
