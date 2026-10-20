namespace SmartLab.BLL.DTOs.Inventory;

public class StockReceiptLineRequest
{
    public Guid ComponentId { get; set; }

    /// <summary>Cabinet the received units are stored in.</summary>
    public Guid CabinetId { get; set; }

    /// <summary>Quantity actually received (thực nhập); only this goes into stock.</summary>
    public int Quantity { get; set; }

    /// <summary>Quantity on the supplier invoice (theo chứng từ); null = same as Quantity.</summary>
    public int? DocumentQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    /// <summary>One entry per unit; required only for HIGH_VALUE components, ignored otherwise.</summary>
    public List<HighValueUnitRequest> Units { get; set; } = new();
}
