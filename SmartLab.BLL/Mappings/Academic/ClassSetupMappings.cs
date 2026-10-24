using SmartLab.BLL.DTOs.ClassSetupDTO;
using SmartLab.DAL.Entities;

namespace SmartLab.BLL.Mappings.Academic;

public static class ClassSetupMappings
{
    public static SemesterResponse ToResponse(Semester s) => new()
    {
        SemesterId = s.SemesterId,
        SemesterCode = s.SemesterCode,
        Term = s.Term,
        Year = s.Year,
        StartDate = s.StartDate,
        EndDate = s.EndDate,
        IsActive = s.IsActive,
    };

    public static CourseResponse ToResponse(Course c) => new()
    {
        CourseId = c.CourseId,
        CourseCode = c.CourseCode,
        CourseName = c.CourseName,
        Credits = c.Credits,
        Description = c.Description,
        IsActive = c.IsActive,
    };

    /// <summary>
    /// Requires: Course, Semester, Instructor.User, ClassStudents to be loaded.
    /// </summary>
    public static ClassResponse ToResponse(Class c) => new()
    {
        ClassId = c.ClassId,
        ClassCode = c.ClassCode,
        CourseId = c.CourseId,
        CourseCode = c.Course.CourseCode,
        CourseName = c.Course.CourseName,
        SemesterId = c.SemesterId,
        SemesterCode = c.Semester.SemesterCode,
        InstructorId = c.InstructorId,
        InstructorName = c.Instructor?.User?.FullName,
        MaxStudents = c.MaxStudents,
        CurrentStudents = c.ClassStudents.Count,
        CreatedAt = c.CreatedAt,
    };
}