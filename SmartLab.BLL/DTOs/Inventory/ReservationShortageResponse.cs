namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>
/// A component whose lendable stock in a lab dropped below what Approved borrow requests of that lab reserve
/// (after units were found broken or lost). Staff should reject or change the affected requests.
/// </summary>
public class ReservationShortageResponse
{
    public Guid ComponentId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;

    /// <summary>Lendable stock left in the lab (CONSUMABLE quantity + Available units).</summary>
    public int InStock { get; set; }

    /// <summary>Quantity reserved by Approved requests of the lab that are not issued yet.</summary>
    public int Reserved { get; set; }

    public List<string> RequestCodes { get; set; } = new();
}
