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

        RuleFor(x => x.ToDate)
            .Must((x, toDate) => x.FromDate!.Value.Date <= toDate!.Value.Date)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày");
    }
}
