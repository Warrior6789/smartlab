namespace SmartLab.BLL.DTOs.ProjectProposals;

public class ProposalComponentResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public int ReservedQuantity { get; set; }
}
