namespace SmartLab.BLL.DTOs.Reports;

public class StockMovementTypeResponse
{
    /// <summary>Receipt, ReceiptCancel, Adjust, Move (between labs only), Retire (QuantityIn = found again), Borrow or Return.</summary>
    public string Type { get; set; } = null!;

    public int QuantityIn { get; set; }
    public int QuantityOut { get; set; }
}
