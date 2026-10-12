namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_requests.status.</summary>
public static class BorrowRequestStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Borrowing = "Borrowing";
    public const string Returned = "Returned";
    public const string Overdue = "Overdue";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All =
        new[] { Pending, Approved, Rejected, Borrowing, Returned, Overdue, Cancelled };

    /// <summary>"pending" / " PENDING " → "Pending"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
