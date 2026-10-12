namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_approvals.status.</summary>
public static class BorrowApprovalStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All = new[] { Pending, Approved, Rejected };
}
