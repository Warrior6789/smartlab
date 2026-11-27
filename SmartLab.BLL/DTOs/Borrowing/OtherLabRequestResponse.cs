namespace SmartLab.BLL.DTOs.Borrowing;

/// <summary>An open request the lab staff searched for that belongs to another lab, so they can send the student there.</summary>
public class OtherLabRequestResponse
{
    public string RequestCode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? StudentCode { get; set; }
    public string RoomCode { get; set; } = string.Empty;
    public string RoomName { get; set; } = string.Empty;
}
