namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>The cabinet code is generated as &lt;room code&gt;-T&lt;nn&gt; and never changes afterwards.</summary>
public class CreateCabinetRequest
{
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string? Location { get; set; }
}
