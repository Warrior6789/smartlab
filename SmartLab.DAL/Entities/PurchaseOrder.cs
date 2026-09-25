using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class PurchaseOrder
{
    public Guid PurchaseOrderId { get; set; }

    public string OrderCode { get; set; } = null!;

    public Guid CreatedBy { get; set; }

    public Guid? ApprovedBy { get; set; }

    public string SupplierName { get; set; } = null!;

    public string Status { get; set; } = null!;

    public decimal TotalAmount { get; set; }

    public DateTime OrderDate { get; set; }

    public DateTime? ReceivedDate { get; set; }

    public string? Note { get; set; }

    public virtual User? ApprovedByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new List<PurchaseOrderItem>();
}
