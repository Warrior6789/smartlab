using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ReceiveTransferRequestValidator : AbstractValidator<ReceiveTransferRequest>
{
    public ReceiveTransferRequestValidator()
    {
        RuleFor(x => x.BorrowItemId)
            .NotEmpty().WithMessage("Phải chọn dòng linh kiện cần nhận");

        RuleFor(x => x.ToCabinetId)
            .NotEmpty().WithMessage("Phải chọn tủ cất ở lab nhận đồ");

        RuleFor(x => x.ItemIds)
            .NotNull().WithMessage("Danh sách mã từng con không được null")
            .Must(ids => ids.Distinct().Count() == ids.Count).WithMessage("Một con chỉ được quét 1 lần");
    }
}
