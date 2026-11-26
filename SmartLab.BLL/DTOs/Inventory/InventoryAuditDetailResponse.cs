namespace SmartLab.BLL.DTOs.Inventory;

public class InventoryAuditDetailResponse : InventoryAuditResponse
{
    /// <summary>Checklist to count, only while InProgress: CONSUMABLE stock of the cabinet.</summary>
    public List<AuditConsumableLineResponse> Consumables { get; set; } = new();

    /// <summary>Checklist to count, only while InProgress: units that should be in the cabinet.</summary>
    public List<AuditItemLineResponse> Items { get; set; } = new();

    /// <summary>Only while InProgress: the counting saved halfway, null when nothing was saved.</summary>
    public AuditDraftResponse? Draft { get; set; }

    /// <summary>Only while Submitted: the consumable counts waiting for approval, with the system quantity at submit.</summary>
    public List<AuditSubmittedCountResponse> SubmittedCounts { get; set; } = new();

    /// <summary>Only while Submitted: units reported missing; they become Lost when the count is approved.</summary>
    public List<AuditItemLineResponse> MissingItems { get; set; } = new();

    /// <summary>Units found that were not on the count list, with what their code says (submitted and completed counts).</summary>
    public List<AuditExtraResponse> Extras { get; set; } = new();

    /// <summary>The audit report once Completed: stock adjustments and lost units written by this audit.</summary>
    public List<InventoryTransactionResponse> Differences { get; set; } = new();

    /// <summary>
    /// Only in the response of approving the count: components the lab no longer has enough of for its Approved requests
    /// because of the counted shortages and lost units.
    /// </summary>
    public List<ReservationShortageResponse> ReservationShortages { get; set; } = new();
}
