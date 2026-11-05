namespace SmartLab.BLL.Constants;

/// <summary>Values of EntityChangedEvent.Entity: which kind of record changed, so FE knows which screens to reload.</summary>
public static class RealtimeEntities
{
    public const string BorrowRequest = "BorrowRequest";
    public const string BorrowExtension = "BorrowExtension";
    public const string Project = "Project";
    public const string Team = "Team";

    /// <summary>Stock moved: receipts, audits, units, consumables, cabinets, or a borrow request that took or returned stock.</summary>
    public const string Inventory = "Inventory";

    /// <summary>Component catalog: components, photos, datasheets, tutorials, categories.</summary>
    public const string Catalog = "Catalog";

    public const string IssueReport = "IssueReport";

    /// <summary>Classes, rosters, courses and semesters.</summary>
    public const string Academic = "Academic";
}
