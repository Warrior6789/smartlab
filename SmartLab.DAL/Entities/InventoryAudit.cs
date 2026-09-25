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

    public virtual User Auditor { get; set; } = null!;

    public virtual StorageCabinet Cabinet { get; set; } = null!;
}
