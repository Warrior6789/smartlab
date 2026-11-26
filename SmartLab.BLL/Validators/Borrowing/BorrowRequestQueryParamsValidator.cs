using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class BorrowRequestQueryParamsValidator : AbstractValidator<BorrowRequestQueryParams>
{
    public BorrowRequestQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => BorrowRequestStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", BorrowRequestStatuses.All)}");
    }
}
