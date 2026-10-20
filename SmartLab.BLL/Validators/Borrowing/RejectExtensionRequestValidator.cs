using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class RejectExtensionRequestValidator : AbstractValidator<RejectExtensionRequest>
{
    public RejectExtensionRequestValidator()
    {
        RuleFor(x => x.Reason)
            .NotEmpty().WithMessage("Lý do từ chối là bắt buộc")
            .MaximumLength(RequestExtensionRequestValidator.ReasonMaxLength)
            .WithMessage($"Lý do tối đa {RequestExtensionRequestValidator.ReasonMaxLength} ký tự");
    }
}
