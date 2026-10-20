using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class ResetPasswordRequestValidator : AbstractValidator<ResetPasswordRequest>
{
    public ResetPasswordRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc")
            .EmailAddress().WithMessage("Email không hợp lệ");

        RuleFor(x => x.Code)
            .NotEmpty().WithMessage("Mã xác thực là bắt buộc")
            .Matches("^[0-9]{6}$").WithMessage("Mã xác thực gồm 6 chữ số");

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc")
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự")
            .MaximumLength(72).WithMessage("Mật khẩu tối đa 72 ký tự")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số");
    }
}
