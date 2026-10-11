using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>multipart/form-data: one invoice file (JPG, PNG, WEBP or PDF).</summary>
public class UploadInvoiceRequest
{
    public IFormFile File { get; set; } = null!;
}
