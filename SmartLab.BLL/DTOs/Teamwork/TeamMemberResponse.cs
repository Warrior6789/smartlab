namespace SmartLab.BLL.DTOs.Teamwork;

public class TeamMemberResponse
{
    public Guid StudentProfileId { get; set; }
    public string StudentCode { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public bool IsLeader { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime InvitedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
}
