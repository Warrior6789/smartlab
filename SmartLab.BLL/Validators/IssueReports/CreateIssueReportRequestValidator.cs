using FluentValidation;

using Microsoft.AspNetCore.Http;

using SmartLab.BLL.Constants;
using SmartLab.BLL.DTOs.IssueReports;

namespace SmartLab.BLL.Validators.IssueReports;

public class CreateIssueReportRequestValidator : AbstractValidator<CreateIssueReportRequest>
{
    private const long MaxImageBytes = 5 * 1024 * 1024;

    private static readonly IReadOnlySet<string> AllowedExtensions =
        new HashSet<string>(new[] { ".jpg", ".jpeg", ".png", ".webp" }, StringComparer.OrdinalIgnoreCase);

    public CreateIssueReportRequestValidator()
    {
        RuleLevelCascadeMode = CascadeMode.Stop;

        RuleFor(x => x.ComponentItemId)
            .Must((request, componentItemId) => componentItemId.HasValue || !string.IsNullOrWhiteSpace(request.ItemCode))
            .WithMessage("Phải cung cấp ComponentItemId hoặc ItemCode");

        RuleFor(x => x.ComponentItemId)
            .NotEqual(Guid.Empty)
            .When(x => x.ComponentItemId.HasValue)
            .WithMessage("ComponentItemId không hợp lệ");

        RuleFor(x => x.ItemCode)
            .Must(itemCode => string.IsNullOrWhiteSpace(itemCode) || itemCode.Trim().Length <= 60)
            .WithMessage("ItemCode không được vượt quá 60 ký tự");

        RuleFor(x => x.IssueType)
            .NotEmpty().WithMessage("Loại sự cố là bắt buộc")
            .Must(issueType => IssueTypes.Normalize(issueType) is not null)
            .WithMessage("Loại sự cố phải là Damaged, Burnt hoặc Lost");

        RuleFor(x => x.Description)
            .Must(description => !string.IsNullOrWhiteSpace(description))
            .WithMessage("Mô tả sự cố là bắt buộc")
            .Must(description => description.Trim().Length <= 1000)
            .WithMessage("Mô tả sự cố không được vượt quá 1000 ký tự");

        When(x => x.Image is not null, () =>
        {
            RuleFor(x => x.Image)
                .Must(image => image!.Length > 0)
                .WithMessage("Ảnh minh chứng không được rỗng")
                .Must(image => image!.Length <= MaxImageBytes)
                .WithMessage("Ảnh minh chứng không được vượt quá 5 MB")
                .Must(HaveAllowedExtension)
                .WithMessage("Ảnh minh chứng phải có định dạng JPG, JPEG, PNG hoặc WEBP")
                .Must(HaveMatchingContentType)
                .WithMessage("MIME type của ảnh minh chứng không khớp định dạng file")
                .MustAsync(HaveValidSignatureAsync)
                .WithMessage("Nội dung ảnh minh chứng không khớp định dạng file");
        });
    }

    private static bool HaveAllowedExtension(IFormFile? image)
        => image is not null && AllowedExtensions.Contains(Path.GetExtension(image.FileName));

    private static bool HaveMatchingContentType(IFormFile? image)
    {
        if (image is null)
            return false;

        return Path.GetExtension(image.FileName).ToLowerInvariant() switch
        {
            ".jpg" or ".jpeg" => image.ContentType.Equals("image/jpeg", StringComparison.OrdinalIgnoreCase),
            ".png" => image.ContentType.Equals("image/png", StringComparison.OrdinalIgnoreCase),
            ".webp" => image.ContentType.Equals("image/webp", StringComparison.OrdinalIgnoreCase),
            _ => false,
        };
    }

    private static async Task<bool> HaveValidSignatureAsync(IFormFile? image, CancellationToken ct)
    {
        if (image is null)
            return true;

        await using Stream stream = image.OpenReadStream();
        long originalPosition = stream.CanSeek ? stream.Position : 0;

        try
        {
            byte[] header = new byte[12];
            int totalRead = 0;
            while (totalRead < header.Length)
            {
                int read = await stream.ReadAsync(header.AsMemory(totalRead, header.Length - totalRead), ct);
                if (read == 0)
                    break;
                totalRead += read;
            }

            string extension = Path.GetExtension(image.FileName);
            return extension.ToLowerInvariant() switch
            {
                ".jpg" or ".jpeg" => totalRead >= 3
                    && header[0] == 0xFF && header[1] == 0xD8 && header[2] == 0xFF,
                ".png" => totalRead >= 8
                    && header.AsSpan(0, 8).SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
                ".webp" => totalRead >= 12
                    && header.AsSpan(0, 4).SequenceEqual("RIFF"u8)
                    && header.AsSpan(8, 4).SequenceEqual("WEBP"u8),
                _ => false,
            };
        }
        finally
        {
            if (stream.CanSeek)
                stream.Position = originalPosition;
        }
    }
}