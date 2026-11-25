using FluentValidation;
using SmartLab.BLL.DTOs.Identity;

namespace SmartLab.BLL.Validators.Identity;

public class ActivateAccountRequestValidator : AbstractValidator<ActivateAccountRequest>
{
    public ActivateAccountRequestValidator()
    {
        RuleFor(x => x.Token)
            .NotEmpty().WithMessage("Link kích hoạt không hợp lệ")
            .MaximumLength(200).WithMessage("Link kích hoạt không hợp lệ");
    }
}
