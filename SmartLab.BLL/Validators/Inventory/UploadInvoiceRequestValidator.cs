using FluentValidation;
using Microsoft.AspNetCore.Http;
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
            .WithMessage("Hóa đơn phải là ảnh JPG, PNG, WEBP hoặc file PDF")
            .Must(HasMatchingSignature).WithMessage("Nội dung file không phải ảnh JPG, PNG, WEBP hoặc PDF hợp lệ");
    }

    /// <summary>The first bytes must match the extension; catches renamed files before they reach the storage service.</summary>
    private static bool HasMatchingSignature(IFormFile file)
    {
        var header = new byte[12];
        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);
        var bytes = header.AsSpan(0, read);

        return Path.GetExtension(file.FileName).ToLowerInvariant() switch
        {
            ".pdf" => bytes.StartsWith("%PDF-"u8),
            ".png" => bytes.StartsWith(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
            ".webp" => read == header.Length && bytes.StartsWith("RIFF"u8) && bytes[8..].SequenceEqual("WEBP"u8),
            _ => bytes.StartsWith(new byte[] { 0xFF, 0xD8, 0xFF }),
        };
    }
}
