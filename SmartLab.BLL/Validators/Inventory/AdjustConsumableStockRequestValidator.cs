using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class AdjustConsumableStockRequestValidator : AbstractValidator<AdjustConsumableStockRequest>
{
    public const int MaxQuantity = 1000000;

    public AdjustConsumableStockRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.CabinetId)
            .NotEmpty().WithMessage("Tủ là bắt buộc");

        RuleFor(x => x.CountedQuantity)
            .InclusiveBetween(0, MaxQuantity).WithMessage($"Số đếm thực tế phải từ 0 đến {MaxQuantity}");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do điều chỉnh là bắt buộc")
            .MaximumLength(255).WithMessage("Lý do tối đa 255 ký tự");
    }
}
