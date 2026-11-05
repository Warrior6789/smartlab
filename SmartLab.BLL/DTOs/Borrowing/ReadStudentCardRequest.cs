using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>multipart/form-data: a photo of the student card. It is read once and not stored.</summary>
public class ReadStudentCardRequest
{
    public IFormFile File { get; set; } = null!;
}
