namespace SmartLab.BLL.DTOs.Academic;

/// <summary>
/// The lecturer lowers how many of one component the team gets. 0 removes the component from the request.
/// Raising a quantity is not possible here: the team withdraws and sends the project again.
/// </summary>
public class ApproveQuantityAdjustmentRequest
{
    public Guid ComponentId { get; set; }
    public int? Quantity { get; set; }
}
