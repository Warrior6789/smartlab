using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class TutorialRequestValidator : AbstractValidator<TutorialRequest>
{
    public TutorialRequestValidator()
    {
        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Tiêu đề là bắt buộc")
            .MaximumLength(200).WithMessage("Tiêu đề tối đa 200 ký tự");

        RuleFor(x => x.Content)
            .NotEmpty().WithMessage("Nội dung là bắt buộc")
            .MaximumLength(20000).WithMessage("Nội dung tối đa 20.000 ký tự");

        RuleFor(x => x.VideoUrl)
            .MaximumLength(500).WithMessage("Link video tối đa 500 ký tự")
            .Must(u => Uri.TryCreate(u, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
            .WithMessage("Link video phải là địa chỉ http/https")
            .When(x => !string.IsNullOrWhiteSpace(x.VideoUrl));
    }
}
