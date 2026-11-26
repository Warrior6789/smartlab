namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// An extra unit of a submitted or completed count, with what its label code says, so the inventory manager knows what to do.
/// Resolution: NoLabel (no code), Unknown (code not in the system), InThisCabinet, OtherCabinet, Lost, Borrowed, Retired.
/// </summary>
public class AuditExtraResponse
{
    public Guid ExtraId { get; set; }
    public string? ItemCode { get; set; }
    public string? Note { get; set; }
    public string Resolution { get; set; } = string.Empty;

    public Guid? ComponentId { get; set; }
    public string? ComponentName { get; set; }

    /// <summary>The unit with that code, when it exists.</summary>
    public Guid? ItemId { get; set; }
    public string? SerialNumber { get; set; }
    public Guid? ItemCabinetId { get; set; }
    public string? ItemCabinetCode { get; set; }
    public string? ItemRoomCode { get; set; }

    /// <summary>The unit's cabinet is in the same lab as the counted cabinet, so it can be moved here.</summary>
    public bool SameLab { get; set; }

    /// <summary>The counted cabinet may hold this component (its tracking type matches or the cabinet takes any), so it can be moved here.</summary>
    public bool FitsThisCabinet { get; set; }

    /// <summary>The open borrow request that still holds the unit (Borrowed).</summary>
    public Guid? BorrowRequestId { get; set; }
    public string? BorrowRequestCode { get; set; }
}
