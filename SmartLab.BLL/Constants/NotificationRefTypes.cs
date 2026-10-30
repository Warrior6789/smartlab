namespace SmartLab.BLL.Constants;

/// <summary>Values of notifications.ref_type: what ref_id points to, so FE knows which screen to open.</summary>
public static class NotificationRefTypes
{
    public const string BorrowRequest = "BorrowRequest";
    public const string BorrowApproval = "BorrowApproval";
    public const string BorrowExtension = "BorrowExtension";
    public const string Project = "Project";
    public const string Team = "Team";
}
