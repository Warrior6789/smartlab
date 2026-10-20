using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class RequestExtensionRequestValidator : AbstractValidator<RequestExtensionRequest>
{
    public const int ReasonMaxLength = 500;

    public RequestExtensionRequestValidator()
    {
        RuleFor(x => x.NewDueDate)
            .NotNull().WithMessage("Ngày trả mới là bắt buộc");

        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do xin gia hạn là bắt buộc")
            .MaximumLength(ReasonMaxLength).WithMessage($"Lý do tối đa {ReasonMaxLength} ký tự");
    }
}
