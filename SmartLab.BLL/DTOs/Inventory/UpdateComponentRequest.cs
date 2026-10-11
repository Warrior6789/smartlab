namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// Replaces the whole component. IsActive and Specs are required so a client that leaves them out
/// does not silently re-activate the component or wipe its specs.
/// </summary>
public class UpdateComponentRequest
{
    public Guid CategoryId { get; set; }
    public string ComponentName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string? Description { get; set; }
    public string? DatasheetUrl { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>Unit of measure (đơn vị tính), e.g. cái, mét, cuộn; empty = "cái".</summary>
    public string? Unit { get; set; }

    /// <summary>Low-stock warning level; null = no warning.</summary>
    public int? MinStock { get; set; }

    /// <summary>Full new spec list; replaces the current specs. Send [] to remove all specs.</summary>
    public List<ComponentSpecRequest>? Specs { get; set; }
}
