using System;
using System.Collections.Generic;

namespace SmartLab.DAL.Entities;

public partial class Component
{
    public Guid ComponentId { get; set; }

    public Guid CategoryId { get; set; }

    public string ComponentCode { get; set; } = null!;

    public string ComponentName { get; set; } = null!;

    public string? Manufacturer { get; set; }

    public string? Description { get; set; }

    public string? DatasheetUrl { get; set; }

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime UpdatedAt { get; set; }

    public decimal UnitPrice { get; set; }

    public string TrackingType { get; set; } = null!;

    public string? DatasheetStorageKey { get; set; }

    public virtual ICollection<BorrowItem> BorrowItems { get; set; } = new List<BorrowItem>();

    public virtual ComponentCategory Category { get; set; } = null!;

    public virtual ICollection<CompatibleComponent> CompatibleComponentCompatibleComponentNavigations { get; set; } = new List<CompatibleComponent>();

    public virtual ICollection<CompatibleComponent> CompatibleComponentComponents { get; set; } = new List<CompatibleComponent>();

    public virtual ICollection<ComponentImage> ComponentImages { get; set; } = new List<ComponentImage>();

    public virtual ICollection<ComponentItem> ComponentItems { get; set; } = new List<ComponentItem>();

    public virtual ICollection<ComponentSpec> ComponentSpecs { get; set; } = new List<ComponentSpec>();

    public virtual ICollection<ComponentTutorial> ComponentTutorials { get; set; } = new List<ComponentTutorial>();

    public virtual ICollection<ConsumableStock> ConsumableStocks { get; set; } = new List<ConsumableStock>();

    public virtual ICollection<InventoryTransaction> InventoryTransactions { get; set; } = new List<InventoryTransaction>();

    public virtual ICollection<ProjectComponent> ProjectComponents { get; set; } = new List<ProjectComponent>();

    public virtual ICollection<StockReceiptItem> StockReceiptItems { get; set; } = new List<StockReceiptItem>();
}
