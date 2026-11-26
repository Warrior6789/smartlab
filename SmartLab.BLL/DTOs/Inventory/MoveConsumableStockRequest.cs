namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>Moves <see cref="Quantity"/> units of one CONSUMABLE component from one cabinet to another.</summary>
public class MoveConsumableStockRequest
{
    public Guid ComponentId { get; set; }
    public Guid FromCabinetId { get; set; }
    public Guid ToCabinetId { get; set; }
    public int Quantity { get; set; }

    /// <summary>Optional, saved in inventory_transactions, e.g. "phiếu nhập chọn nhầm tủ".</summary>
    public string? Reason { get; set; }

    /// <summary>Optional. The stock receipt being corrected, e.g. when it was entered into the wrong cabinet.</summary>
    public Guid? ReceiptId { get; set; }
}
