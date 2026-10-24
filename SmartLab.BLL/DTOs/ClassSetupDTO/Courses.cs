namespace SmartLab.BLL.DTOs.ClassSetupDTO;
public class CreateCourseRequest
{
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateCourseRequest
{
    public string CourseName { get; set; } = null!;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}

public class CourseResponse
{
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }
}