namespace SmartLab.BLL.DTOs.Inventory;

public class InventoryAuditDetailResponse : InventoryAuditResponse
{
    /// <summary>Checklist to count, only while InProgress: CONSUMABLE stock of the cabinet.</summary>
    public List<AuditConsumableLineResponse> Consumables { get; set; } = new();

    /// <summary>Checklist to count, only while InProgress: units that should be in the cabinet.</summary>
    public List<AuditItemLineResponse> Items { get; set; } = new();

    /// <summary>The audit report once Completed: stock adjustments and lost units written by this audit.</summary>
    public List<InventoryTransactionResponse> Differences { get; set; } = new();

    /// <summary>
    /// Only in the response of completing the audit: components the lab no longer has enough of for its Approved requests
    /// because of the counted shortages and lost units.
    /// </summary>
    public List<ReservationShortageResponse> ReservationShortages { get; set; } = new();
}
