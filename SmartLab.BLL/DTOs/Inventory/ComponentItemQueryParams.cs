namespace SmartLab.BLL.DTOs.Inventory;

public class ComponentItemQueryParams
{
    /// <summary>Matches item code or serial number (case-insensitive).</summary>
    public string? Search { get; set; }
    public Guid? ComponentId { get; set; }
    public Guid? CabinetId { get; set; }
    public string? Status { get; set; }

    /// <summary>Only units whose warranty ends between today and today + N days (Lost/Retired units are left out).</summary>
    public int? WarrantyExpiringInDays { get; set; }
}
