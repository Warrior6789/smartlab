using Microsoft.AspNetCore.Http;

namespace SmartLab.BLL.DTOs.ClassSetupDTO;

public class ImportRosterRequest
{
    public IFormFile File { get; set; } = null!;
}
