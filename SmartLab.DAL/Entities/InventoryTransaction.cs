using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class InventoryTransaction
{
    public Guid TransactionId { get; set; }

    public string Type { get; set; } = null!;

    public Guid ComponentId { get; set; }

    public Guid? CabinetId { get; set; }

    public Guid? ItemId { get; set; }

    public int QuantityChange { get; set; }

    public string? Reason { get; set; }

    public Guid? ReceiptId { get; set; }

    public Guid CreatedBy { get; set; }

    public DateTime CreatedAt { get; set; }

    public virtual StorageCabinet? Cabinet { get; set; }

    public virtual Component Component { get; set; } = null!;

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ComponentItem? Item { get; set; }

    public virtual StockReceipt? Receipt { get; set; }
}
