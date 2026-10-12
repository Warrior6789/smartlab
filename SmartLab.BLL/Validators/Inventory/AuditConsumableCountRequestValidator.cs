using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class AuditConsumableCountRequestValidator : AbstractValidator<AuditConsumableCountRequest>
{
    public AuditConsumableCountRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.CountedQuantity)
            .InclusiveBetween(0, AdjustConsumableStockRequestValidator.MaxQuantity)
            .WithMessage($"Số đếm thực tế phải từ 0 đến {AdjustConsumableStockRequestValidator.MaxQuantity}");
    }
}
