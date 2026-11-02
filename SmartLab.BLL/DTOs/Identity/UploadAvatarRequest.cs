using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.Identity;

/// <summary>multipart/form-data: the new avatar image.</summary>
public class UploadAvatarRequest
{
    public IFormFile File { get; set; } = null!;
}
