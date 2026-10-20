using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class VerifyEmailRequestValidator : AbstractValidator<VerifyEmailRequest>
{
    public VerifyEmailRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc")
            .EmailAddress().WithMessage("Email không hợp lệ");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã xác thực là bắt buộc")
            .Matches("^[0-9]{6}$").WithMessage("Mã xác thực gồm 6 chữ số");
    }
}
