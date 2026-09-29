namespace SmartLab.BLL.DTOs.ProjectProposals;

public class SubmitProjectProposalRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IReadOnlyList<ProposalComponentRequest> Components { get; set; } = Array.Empty<ProposalComponentRequest>();
}
