namespace SmartLab.BLL.DTOs.Borrowing;

public class RequestExtensionRequest
{
    /// <summary>New last day to return, Vietnam calendar date (yyyy-MM-dd); after the current due date, at most BORROW_EXTENSION_MAX_DAYS days later.</summary>
    public DateTime? NewDueDate { get; set; }

    public string Reason { get; set; } = string.Empty;
}
