namespace SmartLab.BLL.External.Ai;

/// <summary>One active component of the lab catalog, as shown to the AI.</summary>
public class AiCatalogItem
{
    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    public string CategoryName { get; set; } = string.Empty;

    public string? Description { get; set; }

    public string Unit { get; set; } = string.Empty;

    /// <summary>Quantity available now in the chosen lab.</summary>
    public int Available { get; set; }
}
