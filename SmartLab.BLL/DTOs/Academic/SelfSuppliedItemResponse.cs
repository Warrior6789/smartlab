namespace SmartLab.BLL.DTOs.Academic;

public class SelfSuppliedItemResponse
{
    public Guid ItemId { get; set; }

    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string? Note { get; set; }
}
