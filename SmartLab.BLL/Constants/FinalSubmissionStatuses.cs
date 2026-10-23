namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on projects.final_status.</summary>
public static class FinalSubmissionStatuses
{
    public const string Pending = "Pending";
    public const string Accepted = "Accepted";
    public const string Returned = "Returned";

    public static readonly IReadOnlyList<string> All = new[] { Pending, Accepted, Returned };

    /// <summary>Statuses in which the links are locked: the submission waits for or already has the lecturer's confirmation.</summary>
    public static readonly IReadOnlyList<string> Locked = new[] { Pending, Accepted };

    /// <summary>"pending" / " PENDING " → "Pending"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
