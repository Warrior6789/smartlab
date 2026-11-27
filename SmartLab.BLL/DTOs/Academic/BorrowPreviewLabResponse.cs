namespace SmartLab.BLL.DTOs.Academic;

/// <summary>One future request: the lab and the lines it would lend.</summary>
public class BorrowPreviewLabResponse
{
    public Guid RoomId { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
    public List<BorrowPreviewLineResponse> Lines { get; set; } = new();
}
