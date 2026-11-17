namespace SmartLab.BLL.DTOs.Reports;

public class TopComponentQueryParams
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd) the items were issued, inclusive. Default: 29 days before ToDate.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd), inclusive. Default: today.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Pickup lab of the request.</summary>
    public Guid? RoomId { get; set; }

    public Guid? ClassId { get; set; }

    /// <summary>1 to 50, default 10.</summary>
    public int Top { get; set; } = 10;
}
