namespace SmartLab.BLL.DTOs.ProjectProposals;

public class ReviewProjectProposalRequest
{
    public string Decision { get; set; } = string.Empty;
    public string? RejectReason { get; set; }
}
