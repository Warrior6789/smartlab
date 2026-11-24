using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class ApproveQuantityAdjustmentRequestValidator : AbstractValidator<ApproveQuantityAdjustmentRequest>
{
    public ApproveQuantityAdjustmentRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện điều chỉnh là bắt buộc");
        RuleFor(x => x.Quantity)
            .NotNull().WithMessage("Số lượng duyệt là bắt buộc")
            .GreaterThanOrEqualTo(0).WithMessage("Số lượng duyệt không được âm");
    }
}
