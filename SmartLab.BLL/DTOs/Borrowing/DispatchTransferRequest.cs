namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>The source lab says it sent one line to the pickup lab.</summary>
public class DispatchTransferRequest
{
    public Guid BorrowItemId { get; set; }
}
