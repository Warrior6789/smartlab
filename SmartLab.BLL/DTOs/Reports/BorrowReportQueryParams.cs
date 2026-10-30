namespace SmartLab.BLL.DTOs.Reports;

public class BorrowReportQueryParams
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd), inclusive. Default: 29 days before ToDate.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd), inclusive. Default: today.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Pickup lab of the request.</summary>
    public Guid? RoomId { get; set; }

    public Guid? ClassId { get; set; }
}
