namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_extensions.status.</summary>
public static class BorrowExtensionStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All = new[] { Pending, Approved, Rejected };

    /// <summary>"pending" / " PENDING " → "Pending"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
