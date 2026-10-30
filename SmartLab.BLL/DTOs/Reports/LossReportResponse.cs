namespace SmartLab.BLL.DTOs.Reports;

public class LossReportResponse
{
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }
    public int LostUnits { get; set; }
    public int RetiredUnits { get; set; }
    public int AdjustedOutQuantity { get; set; }
    public decimal TotalValue { get; set; }

    /// <summary>Highest value first.</summary>
    public List<LossLineResponse> Items { get; set; } = new();
}
