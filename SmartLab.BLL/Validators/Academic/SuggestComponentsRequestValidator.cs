using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class SuggestComponentsRequestValidator : AbstractValidator<SuggestComponentsRequest>
{
    public const int DescriptionMinLength = 20;

    public SuggestComponentsRequestValidator()
    {
        RuleFor(x => x.Description)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Mô tả project là bắt buộc")
            .Must(d => d.Trim().Length >= DescriptionMinLength)
            .WithMessage($"Mô tả project tối thiểu {DescriptionMinLength} ký tự để AI gợi ý được")
            .MaximumLength(CreateProjectRequestValidator.DescriptionMaxLength)
            .WithMessage($"Mô tả tối đa {CreateProjectRequestValidator.DescriptionMaxLength} ký tự");
    }
}
