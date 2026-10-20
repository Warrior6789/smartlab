using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class InventoryAuditQueryParamsValidator : AbstractValidator<InventoryAuditQueryParams>
{
    public InventoryAuditQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => InventoryAuditStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", InventoryAuditStatuses.All)}");
    }
}
