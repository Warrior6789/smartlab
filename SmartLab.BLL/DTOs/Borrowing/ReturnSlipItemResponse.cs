namespace SmartLab.BLL.DTOs.Borrowing;

public class ReturnSlipItemResponse
{
    public Guid BorrowItemId { get; set; }

    public Guid ComponentId { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    public string TrackingType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public string? ItemCode { get; set; }

    public string? SerialNumber { get; set; }

    public int BorrowedQuantity { get; set; }

    /// <summary>Quantity the student plans to bring back.</summary>
    public int Quantity { get; set; }

    /// <summary>Issued (still out), Returned, Damaged or Lost.</summary>
    public string LineStatus { get; set; } = string.Empty;

    /// <summary>What the lab really received; null while still out.</summary>
    public int? ReturnedQuantity { get; set; }

    /// <summary>Good, Damaged or Lost (per-unit lines).</summary>
    public string? ReturnCondition { get; set; }

    public DateTime? ReturnedAt { get; set; }
}
