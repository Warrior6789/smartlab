namespace SmartLab.BLL.DTOs.Identity;

public class InstructorProfileResponse
{
    public string InstructorCode { get; set; } = string.Empty;
    public string? Department { get; set; }
    public string? AcademicTitle { get; set; }
}
