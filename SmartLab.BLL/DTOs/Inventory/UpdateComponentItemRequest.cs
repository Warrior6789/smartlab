namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// Corrects the details of one unit. Cabinet and status are changed through the move endpoint instead.
/// SerialNumber is required for HIGH_VALUE units; SerialNumber and HardwareVersion must be empty for other units.
/// </summary>
public class UpdateComponentItemRequest
{
    public string? SerialNumber { get; set; }
    public string? HardwareVersion { get; set; }

    /// <summary>Only the date part is kept.</summary>
    public DateTime? WarrantyExpiresAt { get; set; }

    public string? Condition { get; set; }
    public string? Note { get; set; }
}
