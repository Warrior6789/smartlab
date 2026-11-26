namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on inventory_audits.status.</summary>
public static class InventoryAuditStatuses
{
    /// <summary>Lab staff are counting; stock is not touched yet.</summary>
    public const string InProgress = "InProgress";

    /// <summary>The count was sent and waits for an approver; stock is still not touched.</summary>
    public const string Submitted = "Submitted";

    /// <summary>An approver accepted the count: stock was adjusted and missing units became Lost.</summary>
    public const string Completed = "Completed";

    public static readonly IReadOnlyList<string> All = new[] { InProgress, Submitted, Completed };

    /// <summary>"inprogress" / " INPROGRESS " → "InProgress"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
