using Microsoft.AspNetCore.Http;
using SmartLab.BLL.Constants;

namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>multipart/form-data: file + imageType (Product or Pinout).</summary>
public class UploadComponentImageRequest
{
    public IFormFile File { get; set; } = null!;
    public string ImageType { get; set; } = ComponentImageTypes.Product;
}
