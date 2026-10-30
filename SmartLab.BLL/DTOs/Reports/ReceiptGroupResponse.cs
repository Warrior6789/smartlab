namespace SmartLab.BLL.DTOs.Reports;

public class ReceiptGroupResponse
{
    /// <summary>yyyy-MM, supplier name or category name.</summary>
    public string Key { get; set; } = null!;

    public int ReceiptCount { get; set; }
    public decimal Amount { get; set; }
}
