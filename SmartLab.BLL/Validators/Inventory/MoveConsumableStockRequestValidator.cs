using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class MoveConsumableStockRequestValidator : AbstractValidator<MoveConsumableStockRequest>
{
    public const int MaxQuantity = 1000000;

    public MoveConsumableStockRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.FromCabinetId)
            .NotEmpty().WithMessage("Tủ nguồn là bắt buộc");

        RuleFor(x => x.ToCabinetId)
            .NotEmpty().WithMessage("Tủ đích là bắt buộc")
            .NotEqual(x => x.FromCabinetId).WithMessage("Tủ đích phải khác tủ nguồn");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, MaxQuantity).WithMessage($"Số lượng phải từ 1 đến {MaxQuantity}");

        RuleFor(x => x.Reason)
            .MaximumLength(200).WithMessage("Lý do tối đa 200 ký tự");
    }
}
