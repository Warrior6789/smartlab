using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class InventoryTransactionQueryParamsValidator : AbstractValidator<InventoryTransactionQueryParams>
{
    public InventoryTransactionQueryParamsValidator()
    {
        RuleFor(x => x.Type)
            .Must(t => InventoryTransactionTypes.Normalize(t) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Type))
            .WithMessage($"Loại giao dịch phải là một trong: {string.Join(", ", InventoryTransactionTypes.All)}");

        RuleFor(x => x.ToDate)
            .Must((x, toDate) => x.FromDate!.Value.Date <= toDate!.Value.Date)
            .When(x => x.FromDate.HasValue && x.ToDate.HasValue)
            .WithMessage("Đến ngày phải sau hoặc bằng từ ngày");
    }
}
