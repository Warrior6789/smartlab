namespace SmartLab.BLL.DTOs.Reports;

public class StockValueResponse
{
    public decimal TotalValue { get; set; }

    /// <summary>Units Available or Borrowed at their purchase price.</summary>
    public decimal UnitValue { get; set; }

    /// <summary>Consumable quantity x unit price on the latest completed receipt.</summary>
    public decimal ConsumableValue { get; set; }

    /// <summary>Consumables in stock that were never on a completed receipt, so they count as 0.</summary>
    public int UnpricedComponents { get; set; }

    /// <summary>By category name, highest value first.</summary>
    public List<ValueGroupResponse> ByCategory { get; set; } = new();

    /// <summary>By lab code, highest value first.</summary>
    public List<ValueGroupResponse> ByRoom { get; set; } = new();
}
