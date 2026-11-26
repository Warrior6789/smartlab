namespace SmartLab.BLL.DTOs.ClassSetupDTO;
public class CreateCourseRequest
{
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;

    /// <summary>Lab serving the course: projects of its classes borrow only there. Null = not assigned yet (submitting is blocked).</summary>
    public Guid? RoomId { get; set; }
}

public class UpdateCourseRequest
{
    public string CourseName { get; set; } = null!;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    /// <summary>Lab serving the course; null removes it.</summary>
    public Guid? RoomId { get; set; }
}

public class CourseResponse
{
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public int Credits { get; set; }
    public string? Description { get; set; }
    public bool IsActive { get; set; }

    public Guid? RoomId { get; set; }
}
