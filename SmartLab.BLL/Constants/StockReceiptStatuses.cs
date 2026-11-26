namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on stock_receipts.status.</summary>
public static class StockReceiptStatuses
{
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly IReadOnlyList<string> All = new[] { Completed, Cancelled };

    /// <summary>"completed" / " COMPLETED " → "Completed"; null when the text is not a status.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
