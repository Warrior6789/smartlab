namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on issue_reports.status.</summary>
public static class IssueReportStatuses
{
    public const string Open = "Open";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[] { Open, Confirmed, Rejected, Cancelled };

    /// <summary>"confirmed" / " CONFIRMED " → "Confirmed"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
