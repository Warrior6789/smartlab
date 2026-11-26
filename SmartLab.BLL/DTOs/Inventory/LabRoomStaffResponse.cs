namespace SmartLab.BLL.DTOs.Inventory;

public class LabRoomStaffResponse
{
    public Guid UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public DateTime AssignedAt { get; set; }
}
