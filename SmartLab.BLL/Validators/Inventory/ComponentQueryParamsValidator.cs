using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class ComponentQueryParamsValidator : AbstractValidator<ComponentQueryParams>
{
    public ComponentQueryParamsValidator()
    {
        RuleFor(x => x.TrackingType)
            .Must(t => ComponentTrackingTypes.All.Contains(t!.Trim().ToUpper()))
            .When(x => !string.IsNullOrWhiteSpace(x.TrackingType))
            .WithMessage("Loại linh kiện phải là CONSUMABLE, REUSABLE hoặc HIGH_VALUE");
    }
}
