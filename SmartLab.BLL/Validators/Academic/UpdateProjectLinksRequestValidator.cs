using FluentValidation;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class UpdateProjectLinksRequestValidator : AbstractValidator<UpdateProjectLinksRequest>
{
    private const int UrlMaxLength = 500;

    public UpdateProjectLinksRequestValidator()
    {
        RuleFor(x => x.GithubUrl)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(UrlMaxLength).WithMessage($"Link GitHub tối đa {UrlMaxLength} ký tự")
            .Must(IsHttpUrl).WithMessage("Link GitHub phải là địa chỉ http/https")
            .When(x => !string.IsNullOrWhiteSpace(x.GithubUrl));

        RuleFor(x => x.SlideUrl)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(UrlMaxLength).WithMessage($"Link slide tối đa {UrlMaxLength} ký tự")
            .Must(IsHttpUrl).WithMessage("Link slide phải là địa chỉ http/https")
            .When(x => !string.IsNullOrWhiteSpace(x.SlideUrl));

        RuleFor(x => x.DemoVideoUrl)
            .Cascade(CascadeMode.Stop)
            .MaximumLength(UrlMaxLength).WithMessage($"Link video demo tối đa {UrlMaxLength} ký tự")
            .Must(IsHttpUrl).WithMessage("Link video demo phải là địa chỉ http/https")
            .When(x => !string.IsNullOrWhiteSpace(x.DemoVideoUrl));
    }

    private static bool IsHttpUrl(string? url)
        => Uri.TryCreate(url!.Trim(), UriKind.Absolute, out var uri)
           && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
}
