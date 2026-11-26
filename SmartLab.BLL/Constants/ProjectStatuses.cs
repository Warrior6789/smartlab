namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on projects.status.</summary>
public static class ProjectStatuses
{
    public const string Draft = "Draft";
    public const string Submitted = "Submitted";
    public const string Approved = "Approved";
    public const string Rejected = "Rejected";

    public static readonly IReadOnlyList<string> All = new[] { Draft, Submitted, Approved, Rejected };

    /// <summary>Statuses in which the team can still edit the project and send it for review.</summary>
    public static readonly IReadOnlyList<string> Editable = new[] { Draft, Rejected };

    /// <summary>"draft" / " DRAFT " → "Draft"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
