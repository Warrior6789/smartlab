namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>Drops a replacement not handed over yet (e.g. the free unit turned out broken too).</summary>
public class CancelReplacementRequest
{
    /// <summary>The Pending replacement line.</summary>
    public Guid BorrowItemId { get; set; }

    /// <summary>Emailed to the borrower.</summary>
    public string Reason { get; set; } = string.Empty;
}
