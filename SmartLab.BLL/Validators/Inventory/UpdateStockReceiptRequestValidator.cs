using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class UpdateStockReceiptRequestValidator : AbstractValidator<UpdateStockReceiptRequest>
{
    public UpdateStockReceiptRequestValidator()
    {
        RuleFor(x => x.SupplierName)
            .NotEmpty().WithMessage("Nhà cung cấp là bắt buộc")
            .MaximumLength(150).WithMessage("Nhà cung cấp tối đa 150 ký tự");

        RuleFor(x => x.InvoiceNumber)
            .MaximumLength(50).WithMessage("Số hóa đơn tối đa 50 ký tự");

        RuleFor(x => x.DelivererName)
            .MaximumLength(100).WithMessage("Tên người giao hàng tối đa 100 ký tự");

        RuleFor(x => x.Note)
            .MaximumLength(CreateStockReceiptRequestValidator.NoteMaxLength)
            .WithMessage($"Ghi chú tối đa {CreateStockReceiptRequestValidator.NoteMaxLength} ký tự");

        RuleFor(x => x.ReceivedAt)
            .NotNull().WithMessage("Ngày nhận hàng là bắt buộc");
    }
}
