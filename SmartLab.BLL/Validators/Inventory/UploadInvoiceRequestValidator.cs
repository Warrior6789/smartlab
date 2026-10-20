using FluentValidation;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class UploadInvoiceRequestValidator : AbstractValidator<UploadInvoiceRequest>
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    private static readonly string[] AllowedExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".pdf" };

    public UploadInvoiceRequestValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn file hóa đơn")
            .Must(f => f.Length > 0).WithMessage("File hóa đơn rỗng")
            .Must(f => f.Length <= MaxFileBytes).WithMessage("Hóa đơn tối đa 10 MB")
            .Must(f => AllowedExtensions.Contains(Path.GetExtension(f.FileName).ToLowerInvariant())
                       && (f.ContentType.StartsWith("image/", StringComparison.OrdinalIgnoreCase)
                           || f.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase)))
            .WithMessage("Hóa đơn phải là ảnh JPG, PNG, WEBP hoặc file PDF");
    }
}
