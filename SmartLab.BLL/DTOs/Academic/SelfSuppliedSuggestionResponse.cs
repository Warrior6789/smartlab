namespace SmartLab.BLL.DTOs.Academic;

/// <summary>An item the AI suggests that the lab does not stock, so the team buys it itself.</summary>
public class SelfSuppliedSuggestionResponse
{
    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;
}
