namespace SmartLab.BLL.DTOs.ProjectProposals;

public class ProjectProposalResponse
{
    public Guid ProjectId { get; set; }
    public Guid TeamId { get; set; }
    public Guid ClassId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string ProjectStatus { get; set; } = string.Empty;
    public Guid BorrowRequestId { get; set; }
    public string RequestCode { get; set; } = string.Empty;
    public string BorrowRequestStatus { get; set; } = string.Empty;
    public DateTime SubmittedAt { get; set; }
    public DateTime DueDate { get; set; }
    public IReadOnlyList<ProposalComponentResponse> Components { get; set; } = Array.Empty<ProposalComponentResponse>();
}
