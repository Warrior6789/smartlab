namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentQueryParams
{
    /// <summary>Matches component code, name or manufacturer (case-insensitive).</summary>
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>CONSUMABLE, REUSABLE or HIGH_VALUE.</summary>
    public string? TrackingType { get; set; }

    /// <summary>true = only components whose total quantity is below their MinStock.</summary>
    public bool? LowStock { get; set; }
}
