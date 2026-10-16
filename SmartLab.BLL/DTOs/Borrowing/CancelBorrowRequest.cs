namespace SmartLab.BLL.DTOs.Borrowing;

public class CancelBorrowRequest
{
    /// <summary>Required when staff or an Admin cancels; it is emailed to the borrower. Optional for the borrower.</summary>
    public string? Reason { get; set; }
}
