namespace SmartLab.BLL.DTOs.Identity;

public class InstructorProfileResponse
{
    public string InstructorCode { get; set; } = string.Empty;
    public Guid? DepartmentId { get; set; }
    public string? DepartmentName { get; set; }
    public string? AcademicTitle { get; set; }
}
