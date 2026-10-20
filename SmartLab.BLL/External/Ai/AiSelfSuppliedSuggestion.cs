namespace SmartLab.BLL.External.Ai;

/// <summary>An item the project needs that is not in the lab catalog, so the team buys it itself.</summary>
public class AiSelfSuppliedSuggestion
{
    public string ItemName { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;
}
