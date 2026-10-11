using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class StockReceiptQueryParamsValidator : AbstractValidator<StockReceiptQueryParams>
{
    public StockReceiptQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => StockReceiptStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", StockReceiptStatuses.All)}");
    }
}
