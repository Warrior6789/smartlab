using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class SubmitProjectRequestValidator : AbstractValidator<SubmitProjectRequest>
{
    public const int MaxBorrowDays = 180;

    public SubmitProjectRequestValidator()
    {
        RuleFor(x => x.BorrowDate)
            .NotNull().WithMessage("Ngày mượn là bắt buộc");

        RuleFor(x => x.DueDate)
            .NotNull().WithMessage("Ngày trả là bắt buộc");

        RuleFor(x => x.DueDate)
            .Must((x, dueDate) => dueDate!.Value.Date > x.BorrowDate!.Value.Date)
            .WithMessage("Ngày trả phải sau ngày mượn")
            .Must((x, dueDate) => (dueDate!.Value.Date - x.BorrowDate!.Value.Date).TotalDays <= MaxBorrowDays)
            .WithMessage($"Thời gian mượn tối đa {MaxBorrowDays} ngày")
            .When(x => x.BorrowDate.HasValue && x.DueDate.HasValue);
    }
}
