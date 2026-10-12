namespace SmartLab.BLL.DTOs.Academic;

public class SuggestComponentsRequest
{
    /// <summary>What the project does; the AI reads only this text.</summary>
    public string Description { get; set; } = string.Empty;

    /// <summary>Lab to borrow from: stock is checked in this lab.</summary>
    public Guid RoomId { get; set; }
}
