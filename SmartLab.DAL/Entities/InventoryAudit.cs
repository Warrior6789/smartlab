using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class InventoryAudit
{
    public Guid AuditId { get; set; }

    public Guid CabinetId { get; set; }

    public Guid AuditorId { get; set; }

    public DateTime AuditDate { get; set; }

    public string Status { get; set; } = null!;

    public string? Note { get; set; }

    public DateTime? SubmittedAt { get; set; }

    public Guid? ReviewedBy { get; set; }

    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// Why the reviewer sent the count back (recount), or a note on approval
    /// </summary>
    public string? ReviewNote { get; set; }

    public virtual User Auditor { get; set; } = null!;

    public virtual StorageCabinet Cabinet { get; set; } = null!;

    public virtual ICollection<InventoryAuditCount> InventoryAuditCounts { get; set; } = new List<InventoryAuditCount>();

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual User? ReviewedByNavigation { get; set; }

    public virtual ICollection<ComponentItem> Items { get; set; } = new List<ComponentItem>();
}
