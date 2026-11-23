namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowRequestQueryParams
{
    /// <summary>Request code, project title or borrower name.</summary>
    public string? Search { get; set; }

    /// <summary>Pending, Approved, Ready, Borrowing, Returned, Overdue, Rejected or Cancelled.</summary>
    public string? Status { get; set; }

    /// <summary>true: only requests being borrowed past their due date.</summary>
    public bool? Overdue { get; set; }
}
