namespace SmartLab.BLL.DTOs.ProjectProposals;

public class ProjectProposalReviewDetailResponse
{
    public Guid ProjectId { get; set; }
    public Guid TeamId { get; set; }
    public string TeamName { get; set; } = string.Empty;
    public Guid ClassId { get; set; }
    public string ClassCode { get; set; } = string.Empty;
    public Guid LeaderId { get; set; }
    public string LeaderStudentCode { get; set; } = string.Empty;
    public string LeaderFullName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public Guid BorrowRequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string BorrowRequestStatus { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
    public DateTime SubmittedAt { get; set; }
    public DateTime DueDate { get; set; }
    public IReadOnlyList<ProposalComponentResponse> Components { get; set; } = Array.Empty<ProposalComponentResponse>();
}
