using FluentValidation;
using SmartLab.BLL.DTOs.Settings;

namespace SmartLab.BLL.Validators.Settings;

public class UpdateSystemSettingRequestValidator : AbstractValidator<UpdateSystemSettingRequest>
{
    public UpdateSystemSettingRequestValidator()
    {
        RuleFor(x => x.Value)
            .NotEmpty().WithMessage("Giá trị là bắt buộc")
            .MaximumLength(100).WithMessage("Giá trị tối đa 100 ký tự");
    }
}
