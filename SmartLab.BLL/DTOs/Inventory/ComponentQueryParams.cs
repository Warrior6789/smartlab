namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentQueryParams
{
    /// <summary>Matches component code, name, short code (e.g. CBNDDA-DHT22) or manufacturer (case-insensitive).</summary>
    public string? Search { get; set; }
    public Guid? CategoryId { get; set; }
    public bool? IsActive { get; set; }

    /// <summary>CONSUMABLE, REUSABLE or HIGH_VALUE.</summary>
    public string? TrackingType { get; set; }

    /// <summary>true = only components whose total quantity is below their MinStock.</summary>
    public bool? LowStock { get; set; }

    /// <summary>true = only components some lab has in stock (consumable quantity or an Available unit), what a project can borrow.</summary>
    public bool? InStock { get; set; }
}
