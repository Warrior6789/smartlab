namespace SmartLab.BLL.DTOs.Inventory;

public class InventoryAuditResponse
{
    public Guid AuditId { get; set; }
    public Guid CabinetId { get; set; }
    public string CabinetCode { get; set; } = string.Empty;
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public Guid AuditorId { get; set; }
    public string AuditorName { get; set; } = string.Empty;
    public DateTime AuditDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Note { get; set; }

    /// <summary>When the count was last sent for approval (null while counting for the first time).</summary>
    public DateTime? SubmittedAt { get; set; }

    /// <summary>Approver of a Completed audit, or who last sent the count back for a recount.</summary>
    public Guid? ReviewedById { get; set; }
    public string? ReviewedByName { get; set; }
    public DateTime? ReviewedAt { get; set; }

    /// <summary>Approval note, or the reason for the last recount.</summary>
    public string? ReviewNote { get; set; }

    /// <summary>Number of inventory transactions written by this audit (one per adjusted component or lost unit).</summary>
    public int DifferenceCount { get; set; }
}
