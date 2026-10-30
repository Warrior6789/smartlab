namespace SmartLab.BLL.DTOs.Reports;

public class OverdueItemResponse
{
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string? ItemCode { get; set; }
    public int Quantity { get; set; }
}
