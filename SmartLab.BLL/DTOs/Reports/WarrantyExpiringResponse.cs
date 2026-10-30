namespace SmartLab.BLL.DTOs.Reports;

public class WarrantyExpiringResponse
{
    public Guid ItemId { get; set; }
    public string ItemCode { get; set; } = null!;
    public string? SerialNumber { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string CabinetCode { get; set; } = null!;
    public string RoomCode { get; set; } = null!;

    /// <summary>Available or Borrowed.</summary>
    public string Status { get; set; } = null!;

    public DateTime WarrantyExpiresAt { get; set; }
    public int DaysLeft { get; set; }
}
