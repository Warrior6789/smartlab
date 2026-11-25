namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_return_slips.status.</summary>
public static class ReturnSlipStatuses
{
    public const string Pending = "Pending";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[] { Pending, Approved, Rejected, Completed, Cancelled };

    /// <summary>Waiting for the lab or for the student to bring the goods: their lines cannot be on another open slip.</summary>
    public static readonly string[] Open = { Pending, Approved };

    /// <summary>"pending" / " PENDING " → "Pending"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
