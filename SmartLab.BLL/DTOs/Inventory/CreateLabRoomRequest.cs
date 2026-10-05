namespace SmartLab.BLL.DTOs.Inventory;

/// <summary>The room code is generated as LAB-&lt;nn&gt; and never changes afterwards.</summary>
public class CreateLabRoomRequest
{
    public string RoomName { get; set; } = string.Empty;
}
