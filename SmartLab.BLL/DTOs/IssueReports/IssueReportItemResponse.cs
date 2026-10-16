namespace SmartLab.BLL.DTOs.IssueReports;

public class IssueReportItemResponse
{
    public Guid ComponentItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? SerialNumber { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Condition { get; set; }
    public Guid ComponentId { get; set; }
    public string? ComponentCode { get; set; }
    public string? ComponentName { get; set; }
    public Guid CabinetId { get; set; }
    public string? CabinetCode { get; set; }
    public string? CabinetName { get; set; }
    public string? CabinetLocation { get; set; }
    public Guid? RoomId { get; set; }
    public string? RoomCode { get; set; }
    public string? RoomName { get; set; }
}