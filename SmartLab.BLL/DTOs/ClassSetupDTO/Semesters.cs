namespace SmartLab.BLL.DTOs.ClassSetupDTO;
/// <summary>
/// New semester, starting today or later; it starts active. Term (Spring/Summer/Fall) and Year come from StartDate.
/// SemesterCode empty = made from them (FA26, then FA26_A, FA26_B... when taken).
/// </summary>
public class CreateSemesterRequest
{
    public string? SemesterCode { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
}

/// <summary>The code does not change; Term and Year follow StartDate. IsActive false closes the semester.</summary>
public class UpdateSemesterRequest
{
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class SemesterResponse
{
    public Guid SemesterId { get; set; }
    public string SemesterCode { get; set; } = null!;
    public string Term { get; set; } = null!;
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; }
}