namespace SmartLab.BLL.DTOs.Identity;

public class StudentProfileResponse
{
    public string StudentCode { get; set; } = string.Empty;
    public string? Major { get; set; }
    public string? Cohort { get; set; }
}
