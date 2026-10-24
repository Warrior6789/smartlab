namespace SmartLab.BLL.DTOs.ClassSetupDTO;

public class ImportRosterResult
{
    public int TotalRows { get; set; }
    public int CreatedUsers { get; set; }
    public int EnrolledStudents { get; set; }
    public int SkippedRows { get; set; }
    public List<string> Errors { get; set; } = new();
}


public class AssignInstructorRequest
{
    public Guid ClassId { get; set; }
    public string InstructorCode { get; set; } = null!; 
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? AcademicTitle { get; set; }
}

public class AssignInstructorResponse
{
    public Guid InstructorProfileId { get; set; }
    public string InstructorCode { get; set; } = null!;
    public string FullName { get; set; } = null!;
    public string Email { get; set; } = null!;
    public string? AcademicTitle { get; set; }
}