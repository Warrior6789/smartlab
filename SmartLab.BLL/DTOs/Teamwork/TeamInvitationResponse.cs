namespace SmartLab.BLL.DTOs.Teamwork;

public class TeamInvitationResponse
{
    public Guid TeamId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public Guid LeaderId { get; set; }
    public string LeaderStudentCode { get; set; } = string.Empty;
    public string LeaderFullName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
}
