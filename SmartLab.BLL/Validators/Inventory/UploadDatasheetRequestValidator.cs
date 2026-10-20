using FluentValidation;
using Microsoft.AspNetCore.Http;
using SmartLab.BLL.DTOs.Inventory;

namespace SmartLab.BLL.Validators.Inventory;

public class UploadDatasheetRequestValidator : AbstractValidator<UploadDatasheetRequest>
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    public UploadDatasheetRequestValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn file datasheet")
            .Must(f => f.Length > 0).WithMessage("File datasheet rỗng")
            .Must(f => f.Length <= MaxFileBytes).WithMessage("Datasheet tối đa 10 MB")
            .Must(f => Path.GetExtension(f.FileName).ToLowerInvariant() == ".pdf"
                       && f.ContentType.Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
            .WithMessage("Datasheet phải là file PDF")
            .Must(HasPdfSignature).WithMessage("File không phải PDF hợp lệ");
    }

    /// <summary>Every PDF starts with "%PDF-"; catches renamed files before they reach the storage service.</summary>
    private static bool HasPdfSignature(IFormFile file)
    {
        var signature = "%PDF-"u8.ToArray();
        var header = new byte[signature.Length];

        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        return read == header.Length && header.SequenceEqual(signature);
    }
}
