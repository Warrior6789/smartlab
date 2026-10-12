namespace SmartLab.BLL.DTOs.Academic;

/// <summary>A lab catalog component suggested by the AI that the chosen lab has enough of now.</summary>
public class SuggestedComponentResponse
{
    public Guid ComponentId { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public string ComponentName { get; set; } = string.Empty;

    public string TrackingType { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    /// <summary>Quantity the AI suggests.</summary>
    public int Quantity { get; set; }

    public string Reason { get; set; } = string.Empty;
}
