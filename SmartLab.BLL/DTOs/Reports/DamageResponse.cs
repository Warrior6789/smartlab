namespace SmartLab.BLL.DTOs.Reports;

public class DamageResponse
{
    public Guid BorrowRequestId { get; set; }
    public string RequestCode { get; set; } = null!;
    public string RequesterName { get; set; } = null!;
    public string RequesterUsername { get; set; } = null!;
    public string? ClassCode { get; set; }
    public string ComponentCode { get; set; } = null!;
    public string ComponentName { get; set; } = null!;
    public string? ItemCode { get; set; }

    /// <summary>Damaged or Lost.</summary>
    public string ReturnCondition { get; set; } = null!;

    public DateTime ReturnedAt { get; set; }
}
