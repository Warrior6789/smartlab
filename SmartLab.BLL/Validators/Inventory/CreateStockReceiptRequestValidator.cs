using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class CreateStockReceiptRequestValidator : AbstractValidator<CreateStockReceiptRequest>
{
    public CreateStockReceiptRequestValidator()
    {
        RuleFor(x => x.SupplierName)
            .NotEmpty().WithMessage("Nhà cung cấp là bắt buộc")
            .MaximumLength(150).WithMessage("Nhà cung cấp tối đa 150 ký tự");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(50).WithMessage("Số hóa đơn tối đa 50 ký tự");

        RuleFor(x => x.DelivererName)
            .MaximumLength(100).WithMessage("Tên người giao hàng tối đa 100 ký tự");

        RuleFor(x => x.Items)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Phiếu nhập phải có ít nhất 1 linh kiện")
            .Must(items => items.Where(i => i != null).GroupBy(i => new { i.ComponentId, i.CabinetId }).All(g => g.Count() == 1))
            .WithMessage("Một linh kiện chỉ được xuất hiện 1 lần cho mỗi tủ trong phiếu");

        RuleForEach(x => x.Items)
            .NotNull().WithMessage("Dòng hàng không được null")
            .SetValidator(new StockReceiptLineRequestValidator());
    }
}
