using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class ComponentItem
{
    public Guid ItemId { get; set; }

    public Guid ComponentId { get; set; }

    public Guid CabinetId { get; set; }

    public string? SerialNumber { get; set; }

    public string Status { get; set; } = null!;

    public string? Condition { get; set; }

    public DateTime ImportedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal? PurchasePrice { get; set; }

    public string? HardwareVersion { get; set; }

    public DateTime? WarrantyExpiresAt { get; set; }

    public string? Note { get; set; }

    public Guid? ReceiptItemId { get; set; }

    public virtual ICollection<BorrowItem> BorrowItems { get; set; } = new List<BorrowItem>();

    public virtual StorageCabinet Cabinet { get; set; } = null!;

    public virtual Component Component { get; set; } = null!;

    public virtual ICollection<IssueReport> IssueReports { get; set; } = new List<IssueReport>();

    public virtual StockReceiptItem? ReceiptItem { get; set; }
}
