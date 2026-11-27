namespace SmartLab.BLL.DTOs.Inventory;

public class LabRoomResponse
{
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int CabinetCount { get; set; }
    public int StaffCount { get; set; }

    /// <summary>Components on the lab's list (lab_room_components).</summary>
    public int ComponentCount { get; set; }

    /// <summary>The current user is assigned to this lab as Lab Staff.</summary>
    public bool IsMine { get; set; }

    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
