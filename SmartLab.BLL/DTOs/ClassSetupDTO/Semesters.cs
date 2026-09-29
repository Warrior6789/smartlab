namespace SmartLab.BLL.DTOs.ClassSetupDTO;
public class CreateSemesterRequest
{
    public string SemesterCode { get; set; } = null!;
    public string Term { get; set; } = null!;
    public int Year { get; set; }
    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public bool IsActive { get; set; } = true;
}

public class UpdateSemesterRequest : CreateSemesterRequest { }

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