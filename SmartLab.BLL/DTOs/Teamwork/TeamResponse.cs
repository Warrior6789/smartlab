namespace SmartLab.BLL.DTOs.Teamwork;

public class TeamResponse
{
    public Guid TeamId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public Guid LeaderId { get; set; }
    public bool IsReadyForProject { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<TeamMemberResponse> Members { get; set; } = Array.Empty<TeamMemberResponse>();
}
