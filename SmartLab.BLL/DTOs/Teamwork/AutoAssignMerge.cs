namespace SmartLab.BLL.DTOs.Teamwork;

/// <summary>Suggested merge of a whole team below the minimum into another team.</summary>
public class AutoAssignMerge : TeamMergeItem
{
    public string SourceTeamName { get; set; } = string.Empty;
    public string TargetTeamName { get; set; } = string.Empty;
    public IReadOnlyList<TeamFormationStudentItem> Members { get; set; } = Array.Empty<TeamFormationStudentItem>();
}
