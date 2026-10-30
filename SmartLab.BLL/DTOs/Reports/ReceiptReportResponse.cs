namespace SmartLab.BLL.DTOs.Reports;

public class ReceiptReportResponse
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public string GroupBy { get; set; } = null!;

    /// <summary>Completed receipts in the period.</summary>
    public int ReceiptCount { get; set; }

    public decimal TotalAmount { get; set; }
    public List<ReceiptGroupResponse> Groups { get; set; } = new();
}
