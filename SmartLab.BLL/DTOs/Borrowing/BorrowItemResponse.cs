namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>
/// One line of a borrow request. After handing over, a per-unit line is one unit (Quantity 1, ComponentItemId set).
/// </summary>
public class BorrowItemResponse
{
    public Guid BorrowItemId { get; set; }

    public Guid ComponentId { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    public string TrackingType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public int Quantity { get; set; }

    /// <summary>Pending, Issued, Returned, Damaged or Lost.</summary>
    public string Status { get; set; } = string.Empty;

    /// <summary>Lab the line is taken from (set when the request is approved).</summary>
    public Guid? SourceRoomId { get; set; }

    public string? SourceRoomCode { get; set; }

    /// <summary>When the pickup lab received the line; null while it still has to be carried over.</summary>
    public DateTime? ReceivedAt { get; set; }

    public Guid? ComponentItemId { get; set; }

    public string? ItemCode { get; set; }

    public string? SerialNumber { get; set; }

    public DateTime? IssuedAt { get; set; }

    public DateTime? ReturnedAt { get; set; }

    public int? ReturnedQuantity { get; set; }

    /// <summary>Good, Damaged or Lost (per-unit lines).</summary>
    public string? ReturnCondition { get; set; }

    public Guid? ReturnPhotoId { get; set; }
}
