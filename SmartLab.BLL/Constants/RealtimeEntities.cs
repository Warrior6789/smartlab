namespace SmartLab.BLL.Constants;

/// <summary>Values of EntityChangedEvent.Entity: which kind of record changed, so FE knows which screens to reload.</summary>
public static class RealtimeEntities
{
    public const string BorrowRequest = "BorrowRequest";
    public const string BorrowExtension = "BorrowExtension";
    public const string Project = "Project";
}
