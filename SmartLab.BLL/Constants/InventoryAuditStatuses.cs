namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on inventory_audits.status.</summary>
public static class InventoryAuditStatuses
{
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";

    public static readonly IReadOnlyList<string> All = new[] { InProgress, Completed };

    /// <summary>"inprogress" / " INPROGRESS " → "InProgress"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
