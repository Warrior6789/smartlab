using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.Academic;

/// <summary>multipart/form-data: the final report as one PDF file.</summary>
public class UploadProjectReportRequest
{
    public IFormFile File { get; set; } = null!;
}
