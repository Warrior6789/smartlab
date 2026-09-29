namespace SmartLab.BLL.DTOs.ProjectProposals;

public class ProjectProposalReviewItemResponse
{
    public Guid ProjectId { get; set; }
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string LeaderStudentCode { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public int RequestedComponentTypes { get; set; }
    public int TotalRequestedItems { get; set; }
}
