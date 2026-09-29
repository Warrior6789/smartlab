namespace SmartLab.BLL.DTOs.ClassSetupDTO;
public class CreateClassRequest
{
    public string ClassCode { get; set; } = null!;
    public Guid CourseId { get; set; }
    public Guid SemesterId { get; set; }
    public Guid? InstructorId { get; set; }     
    public int? MaxStudents { get; set; }        
}

public class UpdateClassRequest
{
    public string ClassCode { get; set; } = null!;
    public Guid CourseId { get; set; }
    public int MaxStudents { get; set; }
    public Guid? InstructorId { get; set; }

}

public class ClassResponse
{
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = null!;
    public Guid CourseId { get; set; }
    public string CourseCode { get; set; } = null!;
    public string CourseName { get; set; } = null!;
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = null!;
    public Guid? InstructorId { get; set; }
    public string? InstructorName { get; set; }  
    public int MaxStudents { get; set; }
    public int CurrentStudents { get; set; }
    public DateTime CreatedAt { get; set; }
}