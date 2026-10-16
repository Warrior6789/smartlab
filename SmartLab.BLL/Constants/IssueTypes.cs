namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on issue_reports.issue_type.</summary>
public static class IssueTypes
{
    public const string Damaged = "Damaged";
    public const string Burnt = "Burnt";
    public const string Lost = "Lost";

    public static readonly IReadOnlyList<string> All = new[] { Damaged, Burnt, Lost };

    /// <summary>Returns the stored spelling for a supported issue type; otherwise null.</summary>
    public static string? Normalize(string? issueType) => StatusLookup.Find(All, issueType);
}