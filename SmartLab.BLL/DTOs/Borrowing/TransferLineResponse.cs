namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>A line of an Approved request that still has to be carried from its source lab to the pickup lab.</summary>
public class TransferLineResponse
{
    public Guid BorrowItemId { get; set; }

    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = string.Empty;

    public Guid ComponentId { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    public string TrackingType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public Guid SourceRoomId { get; set; }

    public string SourceRoomCode { get; set; } = string.Empty;

    public Guid PickupRoomId { get; set; }

    public string PickupRoomCode { get; set; } = string.Empty;

    public DateTime? ApprovedAt { get; set; }

    public DateTime? BorrowDate { get; set; }
}
