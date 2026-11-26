namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>One line of a stock receipt, seen from the component.</summary>
public class ComponentReceiptLineResponse
{
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public string CabinetCode { get; set; } = string.Empty;
}
