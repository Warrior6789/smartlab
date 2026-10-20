using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class ComponentItemQueryParamsValidator : AbstractValidator<ComponentItemQueryParams>
{
    public ComponentItemQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => ComponentItemStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", ComponentItemStatuses.All)}");

        RuleFor(x => x.WarrantyExpiringInDays)
            .InclusiveBetween(1, 3650).When(x => x.WarrantyExpiringInDays.HasValue)
            .WithMessage("Số ngày sắp hết bảo hành phải từ 1 đến 3650");
    }
}
