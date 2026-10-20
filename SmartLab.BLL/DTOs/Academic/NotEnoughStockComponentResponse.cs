namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// A suggested catalog component the labs together have less of than suggested, with what they hold now
/// so the team can lower the quantity or buy the rest.
/// </summary>
public class NotEnoughStockComponentResponse : SuggestedComponentResponse
{
    /// <summary>Quantity available now in all labs together (less than <see cref="SuggestedComponentResponse.Quantity"/>).</summary>
    public int Available { get; set; }
}
