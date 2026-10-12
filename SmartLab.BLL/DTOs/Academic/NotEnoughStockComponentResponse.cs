namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// A suggested catalog component the chosen lab has less of than suggested, with what it holds now
/// so the team can lower the quantity or buy the rest.
/// </summary>
public class NotEnoughStockComponentResponse : SuggestedComponentResponse
{
    /// <summary>Quantity available now in the chosen lab (less than <see cref="SuggestedComponentResponse.Quantity"/>).</summary>
    public int AvailableInRoom { get; set; }
}
