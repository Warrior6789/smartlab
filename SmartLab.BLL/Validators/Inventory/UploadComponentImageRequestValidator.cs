using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

/// <summary>One image: JPG/PNG/WEBP, not empty, at most 5 MB. All file errors are reported under "file".</summary>
public class UploadComponentImageRequestValidator : AbstractValidator<UploadComponentImageRequest>
{
    public const long MaxFileBytes = 5 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public UploadComponentImageRequestValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn file ảnh")
            .Must(f => f.Length > 0).WithMessage("File ảnh rỗng")
            .Must(f => f.Length <= MaxFileBytes).WithMessage("Ảnh tối đa 5 MB")
            .Must(f => AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())
                       && f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chỉ nhận ảnh JPG, PNG hoặc WEBP");

        RuleFor(x => x.ImageType)
            .Must(t => ComponentImageTypes.All.Contains(t))
            .WithMessage($"Loại ảnh phải là một trong: {string.Join(", ", ComponentImageTypes.All)}");
    }
}
