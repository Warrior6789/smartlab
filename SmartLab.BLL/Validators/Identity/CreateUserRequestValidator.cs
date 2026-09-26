using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] AllowedRoles =
        { RoleNames.LabStaff, RoleNames.InventoryManager, RoleNames.Lecturer };

    public CreateUserRequestValidator()
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

        RuleFor(x => x.Role)
            .NotEmpty().WithMessage("Role là bắt buộc")
            .Must(r => AllowedRoles.Contains(r))
            .WithMessage($"Role phải là một trong: {string.Join(", ", AllowedRoles)}");

        When(x => x.Role == RoleNames.Lecturer, () =>
        {
            RuleFor(x => x.InstructorCode)
                .NotEmpty().WithMessage("Mã giảng viên là bắt buộc với role Lecturer")
                .MaximumLength(20).WithMessage("Mã giảng viên tối đa 20 ký tự");

            RuleFor(x => x.Department)
                .MaximumLength(100).WithMessage("Khoa tối đa 100 ký tự");

            RuleFor(x => x.AcademicTitle)
                .MaximumLength(50).WithMessage("Học hàm/học vị tối đa 50 ký tự");
        });
    }
}
