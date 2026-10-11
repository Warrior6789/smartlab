using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CabinetQueryParamsValidator : AbstractValidator<CabinetQueryParams>
{
    public CabinetQueryParamsValidator()
    {
        RuleFor(x => x.Status)
            .Must(s => CabinetStatuses.Normalize(s) != null)
            .When(x => !string.IsNullOrWhiteSpace(x.Status))
            .WithMessage($"Trạng thái phải là một trong: {string.Join(", ", CabinetStatuses.All)}");
    }
}
