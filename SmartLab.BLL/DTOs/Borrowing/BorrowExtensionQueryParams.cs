namespace SmartLab.BLL.DTOs.Borrowing;

public class BorrowExtensionQueryParams
{
    /// <summary>Request code, project title or student name.</summary>
    public string? Search { get; set; }

    /// <summary>Pending, Approved or Rejected.</summary>
    public string? Status { get; set; }
}
