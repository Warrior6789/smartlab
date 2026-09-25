using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class StorageCabinet
{
    public Guid CabinetId { get; set; }

    public string CabinetCode { get; set; } = null!;

    public string CabinetName { get; set; } = null!;

    public string LabRoom { get; set; } = null!;

    public string? Location { get; set; }

    public string Status { get; set; } = null!;

    public virtual ICollection<ComponentItem> ComponentItems { get; set; } = new List<ComponentItem>();

    public virtual ICollection<InventoryAudit> InventoryAudits { get; set; } = new List<InventoryAudit>();
}
