namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>The pickup lab confirms it received one line carried over from its source lab.</summary>
public class ReceiveTransferRequest
{
    public Guid BorrowItemId { get; set; }

    /// <summary>Cabinet of the pickup lab where the received components are put.</summary>
    public Guid ToCabinetId { get; set; }

    /// <summary>Per-unit components: exactly the units received (as many as the line quantity). Empty for CONSUMABLE.</summary>
    public List<Guid> ItemIds { get; set; } = new();
}
