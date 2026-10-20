using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class StorageCabinet
{
    public Guid CabinetId { get; set; }

    public string CabinetCode { get; set; } = null!;

    public string CabinetName { get; set; } = null!;

    public string? Location { get; set; }

    public string Status { get; set; } = null!;

    public Guid RoomId { get; set; }

    public virtual ICollection<ComponentItem> ComponentItems { get; set; } = new List<ComponentItem>();

    public virtual ICollection<ConsumableStock> ConsumableStocks { get; set; } = new List<ConsumableStock>();

    public virtual ICollection<InventoryAudit> InventoryAudits { get; set; } = new List<InventoryAudit>();

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual LabRoom Room { get; set; } = null!;

    public virtual ICollection<StockReceiptItem> StockReceiptItems { get; set; } = new List<StockReceiptItem>();
}
