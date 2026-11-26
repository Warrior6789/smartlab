namespace SmartLab.BLL.DTOs.Teamwork;

public class RespondLeaderTransferRequest
{
    /// <summary>Accepted (become the leader) or Rejected.</summary>
    public string Decision { get; set; } = string.Empty;
}
