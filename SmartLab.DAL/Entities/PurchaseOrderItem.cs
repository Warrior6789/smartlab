using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class PurchaseOrderItem
{
    public Guid PurchaseOrderItemId { get; set; }

    public Guid PurchaseOrderId { get; set; }

    public Guid ComponentId { get; set; }

    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal SubTotal { get; set; }

    public int ReceivedQuantity { get; set; }

    public virtual Component Component { get; set; } = null!;

    public virtual ICollection<ComponentItem> ComponentItems { get; set; } = new List<ComponentItem>();

    public virtual PurchaseOrder PurchaseOrder { get; set; } = null!;
}
