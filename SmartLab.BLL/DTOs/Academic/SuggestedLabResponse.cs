namespace SmartLab.BLL.DTOs.Academic;

/// <summary>The lab the project would borrow from: the active lab with Lab Staff that has the most of the suggested components.</summary>
public class SuggestedLabResponse
{
    public Guid RoomId { get; set; }

    public string RoomCode { get; set; } = string.Empty;

    public string RoomName { get; set; } = string.Empty;
}
