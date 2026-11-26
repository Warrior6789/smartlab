using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class UpdateCategoryRequestValidator : AbstractValidator<UpdateCategoryRequest>
{
    public UpdateCategoryRequestValidator()
    {
        RuleFor(x => x.CategoryName)
            .NotEmpty().WithMessage("Tên danh mục là bắt buộc")
            .MaximumLength(100).WithMessage("Tên danh mục tối đa 100 ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(255).WithMessage("Mô tả tối đa 255 ký tự");
    }
}
