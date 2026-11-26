using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>multipart/form-data: one PDF file.</summary>
public class UploadDatasheetRequest
{
    public IFormFile File { get; set; } = null!;
}
