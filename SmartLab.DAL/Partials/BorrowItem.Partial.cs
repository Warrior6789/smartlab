namespace SmartLab.DAL.Entities;

/// <summary>
/// borrow_items.dispatched_at: when the source lab sent a line to the pickup lab. Delete this file after the next
/// scaffold run, which generates the property in Entities/BorrowItem.cs.
/// </summary>
public partial class BorrowItem
{
    public DateTime? DispatchedAt { get; set; }
}
