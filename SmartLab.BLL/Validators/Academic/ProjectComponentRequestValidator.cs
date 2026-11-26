using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class ProjectComponentRequestValidator : AbstractValidator<ProjectComponentRequest>
{
    public const int MaxQuantity = 1000;

    public ProjectComponentRequestValidator()
    {
        RuleFor(x => x.ComponentId)
            .NotEmpty().WithMessage("Linh kiện là bắt buộc");

        RuleFor(x => x.Quantity)
            .InclusiveBetween(1, MaxQuantity).WithMessage($"Số lượng phải từ 1 đến {MaxQuantity}");

        RuleFor(x => x.Note)
            .MaximumLength(255).WithMessage("Ghi chú tối đa 255 ký tự");
    }
}
