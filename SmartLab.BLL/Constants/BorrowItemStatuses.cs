namespace SmartLab.BLL.Constants;

/// <summary>Must match the CHECK constraint on borrow_items.status.</summary>
public static class BorrowItemStatuses
{
    public const string Pending = "Pending";
    public const string Issued = "Issued";
    public const string Returned = "Returned";
    public const string Damaged = "Damaged";
    public const string Lost = "Lost";

    public static readonly IReadOnlyList<string> All = new[] { Pending, Issued, Returned, Damaged, Lost };
}
