using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

/// <summary>
/// Consumable counts submitted for review; system_quantity is the stock when the count was submitted
/// </summary>
public partial class InventoryAuditCount
{
    public Guid AuditId { get; set; }

    public Guid ComponentId { get; set; }

    public int SystemQuantity { get; set; }

    public int CountedQuantity { get; set; }

    public virtual InventoryAudit Audit { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;
}
