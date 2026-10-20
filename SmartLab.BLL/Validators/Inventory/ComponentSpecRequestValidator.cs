using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class ComponentSpecRequestValidator : AbstractValidator<ComponentSpecRequest>
{
    public ComponentSpecRequestValidator()
    {
        RuleFor(x => x.SpecKey)
            .NotEmpty().WithMessage("Tên thông số là bắt buộc")
            .MaximumLength(100).WithMessage("Tên thông số tối đa 100 ký tự");

        RuleFor(x => x.SpecValue)
            .NotEmpty().WithMessage("Giá trị thông số là bắt buộc")
            .MaximumLength(255).WithMessage("Giá trị thông số tối đa 255 ký tự");

        RuleFor(x => x.Unit)
            .MaximumLength(20).WithMessage("Đơn vị tối đa 20 ký tự");
    }
}
