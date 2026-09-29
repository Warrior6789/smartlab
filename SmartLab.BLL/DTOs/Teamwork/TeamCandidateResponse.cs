namespace SmartLab.BLL.DTOs.Teamwork;

public class TeamCandidateResponse
{
    public Guid StudentProfileId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Major { get; set; }
    public string? Cohort { get; set; }
}
