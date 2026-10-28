using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    public UpdateUserRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage("Họ tên là bắt buộc")
            .MaximumLength(100).WithMessage("Họ tên tối đa 100 ký tự");

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage("Email là bắt buộc")
            .MaximumLength(255).WithMessage("Email tối đa 255 ký tự")
            .EmailAddress().WithMessage("Email không hợp lệ");

        RuleFor(x => x.PhoneNumber)
            .MaximumLength(15).WithMessage("Số điện thoại tối đa 15 ký tự")
            .Matches(@"^\+?[0-9]{8,14}$").WithMessage("Số điện thoại không hợp lệ")
            .When(x => !string.IsNullOrWhiteSpace(x.PhoneNumber));
    }
}
