namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>The completed stock receipt entered last for a component, with its lines of that component.</summary>
public class ComponentLatestReceiptResponse
{
    public Guid ReceiptId { get; set; }
    public string ReceiptCode { get; set; } = string.Empty;

    /// <summary>When the receipt was entered into the system (the goods may have arrived earlier).</summary>
    public DateTime CreatedAt { get; set; }

    public IReadOnlyList<ComponentReceiptLineResponse> Lines { get; set; } = Array.Empty<ComponentReceiptLineResponse>();
}
