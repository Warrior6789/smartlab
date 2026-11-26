using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class ActivateUserRequestValidator : AbstractValidator<ActivateUserRequest>
{
    public ActivateUserRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.TemporaryPassword)
            .NotEmpty().WithMessage("Mật khẩu tạm là bắt buộc")
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự")
            .MaximumLength(72).WithMessage("Mật khẩu tối đa 72 ký tự")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số");
    }
}
