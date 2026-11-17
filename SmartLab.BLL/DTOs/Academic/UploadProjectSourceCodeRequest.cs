using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.Academic;

/// <summary>multipart/form-data: the source code as one ZIP file.</summary>
public class UploadProjectSourceCodeRequest
{
    public IFormFile File { get; set; } = null!;
}
