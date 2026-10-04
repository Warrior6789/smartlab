using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class StockReceiptItem
{
    public Guid ReceiptItemId { get; set; }

    public Guid ReceiptId { get; set; }

    public Guid ComponentId { get; set; }

    public Guid CabinetId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public virtual StorageCabinet Cabinet { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;

    public virtual ICollection<ComponentItem> ComponentItems { get; set; } = new List<ComponentItem>();

    public virtual StockReceipt Receipt { get; set; } = null!;
}
