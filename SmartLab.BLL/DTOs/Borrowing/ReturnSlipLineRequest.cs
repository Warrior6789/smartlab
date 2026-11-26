namespace SmartLab.BLL.DTOs.Borrowing;

public class ReturnSlipLineRequest
{
    public Guid BorrowItemId { get; set; }

    /// <summary>CONSUMABLE lines: quantity to bring back, 0 when all of it was used. Not needed for per-unit lines.</summary>
    public int? Quantity { get; set; }
}
