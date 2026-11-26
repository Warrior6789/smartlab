using FluentValidation;
using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.Borrowing;
using SmartLab.BLL.Validators.Inventory;

namespace SmartLab.BLL.Validators.Borrowing;

/// <summary>One image: JPG/PNG/WEBP, not empty, at most 5 MB. All file errors are reported under "file".</summary>
public class UploadHandoverPhotoRequestValidator : AbstractValidator<UploadHandoverPhotoRequest>
{
    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp" };

    public UploadHandoverPhotoRequestValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn file ảnh")
            .Must(f => f.Length > 0).WithMessage("File ảnh rỗng")
            .Must(f => f.Length <= UploadComponentImageRequestValidator.MaxFileBytes).WithMessage("Ảnh tối đa 5 MB")
            .Must(f => AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())
                       && f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Chỉ nhận ảnh JPG, PNG hoặc WEBP");

        RuleFor(x => x.PhotoType)
            .Must(t => HandoverPhotoTypes.All.Contains(t))
            .WithMessage($"Loại ảnh phải là một trong: {string.Join(", ", HandoverPhotoTypes.All)}");
    }
}
