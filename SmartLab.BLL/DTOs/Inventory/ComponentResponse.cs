namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
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

    /// <summary>Warning level set by admin; null = no warning.</summary>
    public int? MinStock { get; set; }

    /// <summary>True when TotalQuantity is below MinStock: time to buy more.</summary>
    public bool IsLowStock => MinStock.HasValue && TotalQuantity < MinStock.Value;

    /// <summary>Unit price on the newest non-cancelled stock receipt; null when the component was never received.</summary>
    public decimal? LatestUnitPrice { get; set; }
}
