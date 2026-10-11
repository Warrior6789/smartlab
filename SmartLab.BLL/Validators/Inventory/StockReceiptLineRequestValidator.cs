using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class StockReceiptLineRequestValidator : AbstractValidator<StockReceiptLineRequest>
{
    public const int MaxQuantity = 10000;

    public StockReceiptLineRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.CabinetId)
            .NotEmpty().WithMessage("Phải chọn tủ cất hàng");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, MaxQuantity).WithMessage($"Số lượng phải từ 1 đến {MaxQuantity}");

        RuleFor(x => x.DocumentQuantity)
            .InclusiveBetween(0, MaxQuantity).When(x => x.DocumentQuantity.HasValue)
            .WithMessage($"Số lượng theo chứng từ phải từ 0 đến {MaxQuantity}");

        RuleFor(x => x.UnitPrice)
            .GreaterThanOrEqualTo(0).WithMessage("Đơn giá không được âm")
            .PrecisionScale(18, 2, true).WithMessage("Đơn giá tối đa 2 chữ số thập phân");

        RuleFor(x => x.Units)
            .NotNull().WithMessage("Danh sách máy không được null");

        RuleForEach(x => x.Units)
            .NotNull().WithMessage("Thông tin từng cái không được null")
            .SetValidator(new HighValueUnitRequestValidator());
    }
}
