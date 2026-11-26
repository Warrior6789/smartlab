using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class InventoryAuditExtra
{
    public Guid ExtraId { get; set; }

    public Guid AuditId { get; set; }

    public string? ItemCode { get; set; }

    public Guid? ComponentId { get; set; }

    public string? Note { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual InventoryAudit Audit { get; set; } = null!;

    public virtual Component? Component { get; set; }
}
