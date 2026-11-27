namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// The component code is generated as LK-&lt;nnnn&gt; and never changes afterwards.
/// Images are uploaded afterwards through POST /api/components/{componentId}/images.
/// </summary>
public class CreateComponentRequest
{
    public Guid CategoryId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }

    /// <summary>Short search code (e.g. CBNDDA-DHT22). Empty = made from the name automatically.</summary>
    public string? ShortCode { get; set; }
    public string? Description { get; set; }
    public string? DatasheetUrl { get; set; }

    /// <summary>CONSUMABLE, REUSABLE or HIGH_VALUE; cannot be changed after creation.</summary>
    public string TrackingType { get; set; } = string.Empty;

    /// <summary>Unit of measure (đơn vị tính), e.g. cái, mét, cuộn; empty = "cái".</summary>
    public string? Unit { get; set; }

    /// <summary>Low-stock warning level, required; 0 = nothing needs to be kept in stock.</summary>
    public int? MinStock { get; set; }
    public List<ComponentSpecRequest> Specs { get; set; } = new();
}
