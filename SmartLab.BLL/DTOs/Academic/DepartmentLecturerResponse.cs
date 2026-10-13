namespace SmartLab.BLL.DTOs.Academic;

public class DepartmentLecturerResponse
{
    public Guid LecturerId { get; set; }
    public string InstructorCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AcademicTitle { get; set; }
    public bool IsHead { get; set; }
}
