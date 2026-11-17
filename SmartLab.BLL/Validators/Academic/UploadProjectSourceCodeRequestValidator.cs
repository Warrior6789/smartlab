using FluentValidation;
using Microsoft.AspNetCore.Http;
using SmartLab.BLL.DTOs.Academic;

namespace SmartLab.BLL.Validators.Academic;

public class UploadProjectSourceCodeRequestValidator : AbstractValidator<UploadProjectSourceCodeRequest>
{
    public const long MaxFileBytes = 10 * 1024 * 1024;

    public UploadProjectSourceCodeRequestValidator()
    {
        RuleFor(x => x.File)
            .Cascade(CascadeMode.Stop)
            .NotNull().WithMessage("Chưa chọn file mã nguồn")
            .Must(f => f.Length > 0).WithMessage("File mã nguồn rỗng")
            .Must(f => f.Length <= MaxFileBytes).WithMessage("Mã nguồn tối đa 10 MB, project lớn hơn hãy dùng link GitHub")
            .Must(f => Path.GetExtension(f.FileName).ToLowerInvariant() == ".zip")
            .WithMessage("Mã nguồn phải là file ZIP")
            .Must(HasZipSignature).WithMessage("File không phải ZIP hợp lệ");
    }

    /// <summary>
    /// A ZIP with files starts with "PK\x03\x04". Browsers send different content types for ZIP
    /// (application/zip, application/x-zip-compressed, application/octet-stream), so the bytes are checked instead.
    /// </summary>
    private static bool HasZipSignature(IFormFile file)
    {
        var signature = new byte[] { 0x50, 0x4B, 0x03, 0x04 };
        var header = new byte[signature.Length];

        using var stream = file.OpenReadStream();
        var read = stream.ReadAtLeast(header, header.Length, throwOnEndOfStream: false);

        return read == header.Length && header.SequenceEqual(signature);
    }
}
