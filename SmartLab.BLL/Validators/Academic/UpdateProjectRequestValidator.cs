using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class UpdateProjectRequestValidator : AbstractValidator<UpdateProjectRequest>
{
    public UpdateProjectRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tên project là bắt buộc")
            .MaximumLength(CreateProjectRequestValidator.TitleMaxLength)
            .WithMessage($"Tên project tối đa {CreateProjectRequestValidator.TitleMaxLength} ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(CreateProjectRequestValidator.DescriptionMaxLength)
            .WithMessage($"Mô tả tối đa {CreateProjectRequestValidator.DescriptionMaxLength} ký tự");

        RuleFor(x => x.Components)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách linh kiện là bắt buộc (gửi [] nếu chưa có)")
            .Must(c => c!.Count <= CreateProjectRequestValidator.MaxComponents)
            .WithMessage($"Tối đa {CreateProjectRequestValidator.MaxComponents} linh kiện")
            .Must(c => c!.Where(x => x != null).GroupBy(x => x.ComponentId).All(g => g.Count() == 1))
            .WithMessage("Một linh kiện chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.Components)
            .NotNull().WithMessage("Linh kiện không được null")
            .SetValidator(new ProjectComponentRequestValidator());

        RuleFor(x => x.SelfSuppliedItems)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách món tự mua là bắt buộc (gửi [] nếu không có)")
            .Must(i => i!.Count <= SelfSuppliedItemRequestValidator.MaxItems)
            .WithMessage($"Tối đa {SelfSuppliedItemRequestValidator.MaxItems} món tự mua")
            .Must(i => SelfSuppliedItemRequestValidator.HasUniqueNames(i!))
            .WithMessage("Một món tự mua chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.SelfSuppliedItems)
            .NotNull().WithMessage("Món tự mua không được null")
            .SetValidator(new SelfSuppliedItemRequestValidator());
    }
}
