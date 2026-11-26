using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class HighValueUnitRequestValidator : AbstractValidator<HighValueUnitRequest>
{
    public HighValueUnitRequestValidator()
    {
        RuleFor(x => x.SerialNumber)
            .NotEmpty().WithMessage("Serial là bắt buộc")
            .MaximumLength(100).WithMessage("Serial tối đa 100 ký tự");

        RuleFor(x => x.HardwareVersion)
            .MaximumLength(50).WithMessage("Version tối đa 50 ký tự");

        RuleFor(x => x.Note)
            .MaximumLength(255).WithMessage("Ghi chú tối đa 255 ký tự");
    }
}
