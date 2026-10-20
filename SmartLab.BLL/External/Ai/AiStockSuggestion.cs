namespace SmartLab.BLL.External.Ai;

/// <summary>A catalog component the AI thinks the project needs, referenced by its code (e.g. LK-0003).</summary>
public class AiStockSuggestion
{
    public string ComponentCode { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;
}
