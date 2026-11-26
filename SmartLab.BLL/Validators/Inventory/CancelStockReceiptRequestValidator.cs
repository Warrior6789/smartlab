using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CancelStockReceiptRequestValidator : AbstractValidator<CancelStockReceiptRequest>
{
    public CancelStockReceiptRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do hủy phiếu là bắt buộc")
            .MaximumLength(255).WithMessage("Lý do tối đa 255 ký tự");
    }
}
