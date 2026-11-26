namespace SmartLab.BLL.DTOs.Academic;

/// <summary>An item the team buys itself because the lab does not stock it.</summary>
public class SelfSuppliedItemRequest
{
    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Note { get; set; }
}
