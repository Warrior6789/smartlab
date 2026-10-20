using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

/// <summary>The line's tracking type decides which field is used; the service checks that.</summary>
public class ReturnLineRequestValidator : AbstractValidator<ReturnLineRequest>
{
    public ReturnLineRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Phải chọn dòng linh kiện");

        RuleFor(x => x.Condition)
            .Must(c => ReturnConditions.Normalize(c) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Condition))
            .WithMessage($"Tình trạng phải là một trong: {string.Join(", ", ReturnConditions.All)}");

        RuleFor(x => x.ReturnedQuantity)
            .GreaterThanOrEqualTo(0).WithMessage("Số lượng trả không được âm")
            .When(x => x.ReturnedQuantity.HasValue);
    }
}
