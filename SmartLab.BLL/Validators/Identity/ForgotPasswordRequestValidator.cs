using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class ForgotPasswordRequestValidator : AbstractValidator<ForgotPasswordRequest>
{
    public ForgotPasswordRequestValidator()
    {
        RuleFor(x => x.Email)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Email là bắt buộc")
            .EmailAddress().WithMessage("Email không hợp lệ");
    }
}
