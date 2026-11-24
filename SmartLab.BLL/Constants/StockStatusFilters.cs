namespace SmartLab.BLL.Constants;

/// <summary>Values of the status filter of the stock summary report.</summary>
public static class StockStatusFilters
{
    public const string Stocked = "Stocked";
    public const string OutOfStock = "OutOfStock";
    public const string LowStock = "LowStock";

    public static readonly IReadOnlyList<string> All = new[] { Stocked, OutOfStock, LowStock };

    /// <summary>"lowstock" / " LowStock " → "LowStock"; null when the text is not a filter.</summary>
    public static string? Normalize(string? status) => StatusLookup.Find(All, status);
}
