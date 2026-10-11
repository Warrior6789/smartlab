namespace SmartLab.BLL.DTOs.Inventory;

public class StockReceiptQueryParams
{
    /// <summary>Matches receipt code or supplier name (case-insensitive).</summary>
    public string? Search { get; set; }

    /// <summary>Completed or Cancelled.</summary>
    public string? Status { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd) of ReceivedAt, inclusive.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd) of ReceivedAt, inclusive.</summary>
    public DateTime? ToDate { get; set; }
}
