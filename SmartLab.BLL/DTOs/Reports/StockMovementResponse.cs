namespace SmartLab.BLL.DTOs.Reports;

public class StockMovementResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string Unit { get; set; } = null!;
    public DateTime FromDate { get; set; }
    public DateTime ToDate { get; set; }

    /// <summary>Sum of all ledger changes before FromDate (only correct from when the ledger started).</summary>
    public int OpeningBalance { get; set; }

    public int TotalIn { get; set; }
    public int TotalOut { get; set; }

    /// <summary>OpeningBalance + TotalIn - TotalOut.</summary>
    public int ClosingBalance { get; set; }

    public List<StockMovementTypeResponse> Types { get; set; } = new();
}
