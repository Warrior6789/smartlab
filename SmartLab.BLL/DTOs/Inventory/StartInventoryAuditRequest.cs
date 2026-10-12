namespace SmartLab.BLL.DTOs.Inventory;

public class StartInventoryAuditRequest
{
    public Guid CabinetId { get; set; }
    public string? Note { get; set; }
}
