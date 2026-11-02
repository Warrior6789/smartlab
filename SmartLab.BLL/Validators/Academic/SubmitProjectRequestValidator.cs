using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class SubmitProjectRequestValidator : AbstractValidator<SubmitProjectRequest>
{
    public SubmitProjectRequestValidator()
    {
        RuleFor(x => x.BorrowDate)
            .NotNull().WithMessage("Ngày mượn là bắt buộc khi đã chọn ngày trả")
            .When(x => x.DueDate.HasValue);

        RuleFor(x => x.DueDate)
            .NotNull().WithMessage("Ngày trả là bắt buộc khi đã chọn ngày mượn")
            .When(x => x.BorrowDate.HasValue);

        RuleFor(x => x.DueDate)
            .Must((x, dueDate) => dueDate!.Value.Date > x.BorrowDate!.Value.Date)
            .WithMessage("Ngày trả phải sau ngày mượn")
            .When(x => x.BorrowDate.HasValue && x.DueDate.HasValue);
    }
}
