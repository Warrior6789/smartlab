namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Sends a submitted count back to the lab staff to count again; the reason tells them what to check.</summary>
public class RecountInventoryAuditRequest
{
    public string Reason { get; set; } = string.Empty;
}
