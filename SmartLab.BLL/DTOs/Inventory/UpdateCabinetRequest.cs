namespace SmartLab.BLL.DTOs.Inventory;

public class UpdateCabinetRequest
{
    public string CabinetName { get; set; } = string.Empty;
    public Guid RoomId { get; set; }
    public string? Location { get; set; }
    public string Status { get; set; } = string.Empty;
}
