namespace SmartLab.BLL.DTOs.Borrowing;

public class ReturnLineRequest
{
    public Guid BorrowItemId { get; set; }

    /// <summary>Per-unit lines: Good, Damaged or Lost.</summary>
    public string? Condition { get; set; }

    /// <summary>CONSUMABLE lines: quantity brought back (0 to the line quantity); the rest counts as used up.</summary>
    public int? ReturnedQuantity { get; set; }
}
