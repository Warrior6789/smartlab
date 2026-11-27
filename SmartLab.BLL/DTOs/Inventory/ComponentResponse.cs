namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentResponse
{
    public Guid ComponentId { get; set; }
    /// <summary>System code (LK-0239): never changes, kept for searching and old Excel files. Screens show ShortCode instead.</summary>
    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    /// <summary>
    /// Code shown to users, made from the name like drug abbreviations (e.g. CBNDDA-DHT22), unique. Other responses return
    /// this one as ComponentCode.
    /// </summary>
    public string? ShortCode { get; set; }
    public Guid CategoryId { get; set; }
    public string CategoryName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? PrimaryImageUrl { get; set; }
    public bool IsActive { get; set; }
    public string TrackingType { get; set; } = string.Empty;

    /// <summary>Unit of measure (đơn vị tính), e.g. cái, mét, cuộn.</summary>
    public string Unit { get; set; } = string.Empty;
    public int TotalQuantity { get; set; }
    public int AvailableQuantity { get; set; }

    /// <summary>What can still be borrowed: AvailableQuantity minus what approved requests hold, as checked when sending one.</summary>
    public int BorrowableQuantity { get; set; }

    /// <summary>Warning level set by admin; null = no warning.</summary>
    public int? MinStock { get; set; }

    /// <summary>True when TotalQuantity is below MinStock: time to buy more.</summary>
    public bool IsLowStock => MinStock.HasValue && TotalQuantity < MinStock.Value;

    /// <summary>
    /// Unit price on the newest completed stock receipt that was paid for: gifts and units found during a count (0₫) are skipped.
    /// Null when the component was never bought.
    /// </summary>
    public decimal? LatestUnitPrice { get; set; }

    /// <summary>When the component was added to the catalog.</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Last receipt entered for this component; only for users who manage stock receipts, null when never received.</summary>
    public ComponentLatestReceiptResponse? LatestReceipt { get; set; }

    /// <summary>Codes of the labs that take in this component (lab_room_components); empty = no lab yet, cannot be received.</summary>
    public IReadOnlyList<string> RoomCodes { get; set; } = Array.Empty<string>();
}
