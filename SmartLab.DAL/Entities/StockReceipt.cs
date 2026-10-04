using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class StockReceipt
{
    public Guid ReceiptId { get; set; }

    public string ReceiptCode { get; set; } = null!;

    public string SupplierName { get; set; } = null!;

    public DateTime ReceivedAt { get; set; }

    public Guid CreatedBy { get; set; }

    public decimal TotalAmount { get; set; }

    public string? Note { get; set; }

    public string Status { get; set; } = null!;

    public DateTime CreatedAt { get; set; }

    public DateTime? CancelledAt { get; set; }

    public Guid? CancelledBy { get; set; }

    public string? CancelReason { get; set; }

    public virtual User? CancelledByNavigation { get; set; }

    public virtual User CreatedByNavigation { get; set; } = null!;

    public virtual ICollection<StockReceiptItem> StockReceiptItems { get; set; } = new List<StockReceiptItem>();
}
