using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class BorrowExtensionQueryParamsValidator : AbstractValidator<BorrowExtensionQueryParams>
{
    public BorrowExtensionQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => BorrowExtensionStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", BorrowExtensionStatuses.All)}");
    }
}
