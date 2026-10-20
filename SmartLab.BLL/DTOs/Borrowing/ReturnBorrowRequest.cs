namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>Records one return at the pickup lab. A request may be returned in several times.</summary>
public class ReturnBorrowRequest
{
    /// <summary>The Return photo uploaded for this return.</summary>
    public Guid PhotoId { get; set; }

    /// <summary>Cabinet of the pickup lab for returned CONSUMABLE quantities; required when any is returned.</summary>
    public Guid? CabinetId { get; set; }

    public List<ReturnLineRequest> Lines { get; set; } = new();
}
