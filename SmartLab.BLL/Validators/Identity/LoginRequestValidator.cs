using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Email hoặc username là bắt buộc");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu là bắt buộc");
    }
}
