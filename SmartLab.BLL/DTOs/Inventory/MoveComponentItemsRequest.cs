namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// Moves units of one component that are in <see cref="FromCabinetId"/> with <see cref="FromStatus"/>
/// to another cabinet and/or status. Null target fields keep the current value.
/// Send either <see cref="ItemIds"/> (exact units, e.g. one serial) or <see cref="Quantity"/> (the system picks the oldest units), not both.
/// </summary>
public class MoveComponentItemsRequest
{
    public Guid ComponentId { get; set; }
    public Guid FromCabinetId { get; set; }
    public string FromStatus { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public List<Guid>? ItemIds { get; set; }
    public Guid? ToCabinetId { get; set; }
    public string? ToStatus { get; set; }

    /// <summary>Optional, saved in inventory_transactions, e.g. "phiếu nhập chọn nhầm tủ".</summary>
    public string? Reason { get; set; }

    /// <summary>Optional. The stock receipt being corrected, e.g. when it was entered into the wrong cabinet.</summary>
    public Guid? ReceiptId { get; set; }
}
