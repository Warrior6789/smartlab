using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class CreateProjectRequestValidator : AbstractValidator<CreateProjectRequest>
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 5000;
    public const int MaxComponents = 100;

    public CreateProjectRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tên project là bắt buộc")
            .MaximumLength(TitleMaxLength).WithMessage($"Tên project tối đa {TitleMaxLength} ký tự");

        RuleFor(x => x.Description)
            .MaximumLength(DescriptionMaxLength).WithMessage($"Mô tả tối đa {DescriptionMaxLength} ký tự");

        RuleFor(x => x.Components)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách linh kiện không được null")
            .Must(c => c.Count <= MaxComponents).WithMessage($"Tối đa {MaxComponents} linh kiện")
            .Must(c => c.Where(x => x != null).GroupBy(x => x.ComponentId).All(g => g.Count() == 1))
            .WithMessage("Một linh kiện chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.Components)
            .NotNull().WithMessage("Linh kiện không được null")
            .SetValidator(new ProjectComponentRequestValidator());

        RuleFor(x => x.SelfSuppliedItems)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Danh sách món tự mua không được null")
            .Must(i => i.Count <= SelfSuppliedItemRequestValidator.MaxItems)
            .WithMessage($"Tối đa {SelfSuppliedItemRequestValidator.MaxItems} món tự mua")
            .Must(SelfSuppliedItemRequestValidator.HasUniqueNames)
            .WithMessage("Một món tự mua chỉ được xuất hiện 1 lần");

        RuleForEach(x => x.SelfSuppliedItems)
            .NotNull().WithMessage("Món tự mua không được null")
            .SetValidator(new SelfSuppliedItemRequestValidator());
    }
}
