using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.Username)
            .NotEmpty().WithMessage("Username là bắt buộc")
            .Length(3, 50).WithMessage("Username phải từ 3 đến 50 ký tự")
            .Matches("^[a-zA-Z0-9._]+$").WithMessage("Username chỉ gồm chữ, số, dấu chấm và gạch dưới");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc")
            .MaximumLength(255).WithMessage("Email tối đa 255 ký tự")
            .EmailAddress().WithMessage("Email không hợp lệ");

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu là bắt buộc")
            .MinimumLength(8).WithMessage("Mật khẩu tối thiểu 8 ký tự")
            .MaximumLength(72).WithMessage("Mật khẩu tối đa 72 ký tự")
            .Matches("[A-Z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ hoa")
            .Matches("[a-z]").WithMessage("Mật khẩu phải có ít nhất 1 chữ thường")
            .Matches("[0-9]").WithMessage("Mật khẩu phải có ít nhất 1 chữ số");

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên là bắt buộc")
            .MaximumLength(100).WithMessage("Họ tên tối đa 100 ký tự");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(15).WithMessage("Số điện thoại tối đa 15 ký tự")
            .Matches(@"^\+?[0-9]{8,14}$").WithMessage("Số điện thoại không hợp lệ")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));

        RuleFor(x => x.StudentCode)
            .NotEmpty().WithMessage("Mã sinh viên là bắt buộc")
            .MaximumLength(20).WithMessage("Mã sinh viên tối đa 20 ký tự");

        RuleFor(x => x.Major)
            .MaximumLength(100).WithMessage("Chuyên ngành tối đa 100 ký tự");

        RuleFor(x => x.Cohort)
            .MaximumLength(10).WithMessage("Khóa tối đa 10 ký tự");
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Identifier).NotEmpty().WithMessage("Email hoặc username là bắt buộc");
        RuleFor(x => x.Password).NotEmpty().WithMessage("Mật khẩu là bắt buộc");
    }
}
