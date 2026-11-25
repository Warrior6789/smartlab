namespace SmartLab.BLL.DTOs.Borrowing;

public class ReturnSlipDetailResponse : ReturnSlipResponse
{
    public string? Note { get; set; }

    public Guid CreatedBy { get; set; }

    public string CreatedByName { get; set; } = string.Empty;

    public Guid? ReviewedBy { get; set; }

    public string? ReviewedByName { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>Note of the lab staff, or why they rejected.</summary>
    public string? ReviewComment { get; set; }

    public Guid? ReceivedBy { get; set; }

    public string? ReceivedByName { get; set; }

    public DateTime? ReceivedAt { get; set; }

    /// <summary>The caller sent the request or leads its team, and the goods were not received yet.</summary>
    public bool CanCancel { get; set; }

    public List<ReturnSlipItemResponse> Items { get; set; } = new();
}
