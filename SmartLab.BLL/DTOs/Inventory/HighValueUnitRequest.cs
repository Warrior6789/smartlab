namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Details of one received unit of a HIGH_VALUE component.</summary>
public class HighValueUnitRequest
{
    public string SerialNumber { get; set; } = string.Empty;
    public string? HardwareVersion { get; set; }

    /// <summary>Only the date part is kept.</summary>
    public DateTime? WarrantyExpiresAt { get; set; }

    public string? Note { get; set; }
}
