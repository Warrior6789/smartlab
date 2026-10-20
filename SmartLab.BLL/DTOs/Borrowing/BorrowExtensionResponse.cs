namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowExtensionResponse
{
    public Guid ExtensionId { get; set; }

    public Guid BorrowRequestId { get; set; }

    public string RequestCode { get; set; } = string.Empty;

    public string RequesterName { get; set; } = string.Empty;

    public string? ClassCode { get; set; }

    public string? ProjectTitle { get; set; }

    /// <summary>Due date when the extension was asked.</summary>
    public DateTime OldDueDate { get; set; }

    public DateTime NewDueDate { get; set; }

    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public Guid? ReviewedBy { get; set; }

    public string? ReviewedByName { get; set; }

    /// <summary>Lecturer's note when approving, or why it was rejected.</summary>
    public string? ReviewComment { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ReviewedAt { get; set; }
}
