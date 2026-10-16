using Microsoft.AspNetCore.Http;
using SmartLab.BLL.Constants;

namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>multipart/form-data: file + photoType (Issue or Return). The photo shows the components and the student card.</summary>
public class UploadHandoverPhotoRequest
{
    public IFormFile File { get; set; } = null!;

    public string PhotoType { get; set; } = HandoverPhotoTypes.Issue;
}
