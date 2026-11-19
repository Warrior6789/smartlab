namespace SmartLab.BLL.DTOs.Teamwork;

public class TeamResponse
{
    public Guid TeamId { get; set; }
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string TeamName { get; set; } = string.Empty;
    public Guid LeaderId { get; set; }
    /// <summary>Class team formation is Locked, no Pending invitation and at least MinTeamSize Accepted members.</summary>
    public bool IsReadyForProject { get; set; }
    public string TeamFormationStatus { get; set; } = string.Empty;
    public int? MinTeamSize { get; set; }
    public int? MaxTeamSize { get; set; }
    /// <summary>Accepted members, leader included.</summary>
    public int MemberCount { get; set; }
    /// <summary>Accepted + Pending reached MaxTeamSize: the leader cannot invite anyone else.</summary>
    public bool IsFull { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public IReadOnlyList<TeamMemberResponse> Members { get; set; } = Array.Empty<TeamMemberResponse>();
}
