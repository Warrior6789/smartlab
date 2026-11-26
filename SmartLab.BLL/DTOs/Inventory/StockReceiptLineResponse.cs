namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>One row of the receipt table, in the order of form 01-VT.</summary>
public class StockReceiptLineResponse
{
    /// <summary>Row number (STT), starting at 1.</summary>
    public int LineNo { get; set; }

    public Guid ReceiptItemId { get; set; }
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public string? Manufacturer { get; set; }
    public string TrackingType { get; set; } = string.Empty;

    /// <summary>Unit of measure (đơn vị tính), e.g. cái, mét, cuộn.</summary>
    public string Unit { get; set; } = string.Empty;

    public Guid CabinetId { get; set; }
    public string CabinetCode { get; set; } = string.Empty;

    /// <summary>Quantity on the supplier invoice (theo chứng từ).</summary>
    public int DocumentQuantity { get; set; }

    /// <summary>Quantity actually received (thực nhập).</summary>
    public int Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>Quantity × UnitPrice (thành tiền theo số thực nhập).</summary>
    public decimal SubTotal { get; set; }

    /// <summary>Serial details of the HIGH_VALUE units received on this line; empty for other types and for cancelled receipts.</summary>
    public IReadOnlyList<StockReceiptUnitResponse> Units { get; set; } = Array.Empty<StockReceiptUnitResponse>();
}
