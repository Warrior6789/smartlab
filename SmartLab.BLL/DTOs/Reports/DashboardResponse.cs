namespace SmartLab.BLL.DTOs.Reports;

/// <summary>Home page tiles; a tile is null when the current user's role does not see it.</summary>
public class DashboardResponse
{
    /// <summary>Active components (Admin, InventoryManager, LabStaff).</summary>
    public int? TotalComponents { get; set; }

    /// <summary>Active components that are or were kept in the labs the user sees (Admin, InventoryManager, LabStaff).</summary>
    public int? StockedComponents { get; set; }

    /// <summary>Components below MinStock in the labs the user sees (Admin, InventoryManager, LabStaff).</summary>
    public int? LowStockCount { get; set; }

    /// <summary>Components kept in those labs with nothing available in the labs the user sees (Admin, InventoryManager, LabStaff).</summary>
    public int? OutOfStockCount { get; set; }

    /// <summary>Components below MinStock that still have some available (Admin, InventoryManager, LabStaff).</summary>
    public int? BelowMinStockCount { get; set; }

    /// <summary>Units in stock or lent out at purchase price, plus consumables at the latest receipt price (Admin, InventoryManager).</summary>
    public decimal? StockValue { get; set; }

    /// <summary>Completed stock receipts this month, Vietnam time (Admin, InventoryManager).</summary>
    public int? ReceiptsThisMonth { get; set; }

    /// <summary>Total amount of those receipts (Admin, InventoryManager).</summary>
    public decimal? ReceiptAmountThisMonth { get; set; }

    /// <summary>Admin, InventoryManager: all pending requests. Lecturer: requests waiting for their approval.</summary>
    public int? PendingApprovals { get; set; }

    /// <summary>Requests ready to hand over (Admin, InventoryManager, LabStaff).</summary>
    public int? ReadyToIssue { get; set; }

    /// <summary>Requests being borrowed, overdue included.</summary>
    public int? BorrowingCount { get; set; }

    public int? OverdueCount { get; set; }
}
