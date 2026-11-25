namespace SmartLab.BLL.DTOs.Teamwork;

/// <summary>A team below the minimum that is dissolved: all its members join the target team.</summary>
public class TeamMergeItem
{
    public Guid SourceTeamId { get; set; }
    public Guid TargetTeamId { get; set; }
}
