using FluentValidation;
using SmartLab.BLL.DTOs.Borrowing;

namespace SmartLab.BLL.Validators.Borrowing;

public class ApproveExtensionRequestValidator : AbstractValidator<ApproveExtensionRequest>
{
    public ApproveExtensionRequestValidator()
    {
        RuleFor(x => x.Comment)
            .MaximumLength(RequestExtensionRequestValidator.ReasonMaxLength)
            .WithMessage($"Ghi chú tối đa {RequestExtensionRequestValidator.ReasonMaxLength} ký tự");
    }
}
