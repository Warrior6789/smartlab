namespace SmartLab.BLL.DTOs.Reports;

public class ReceiptReportQueryParams
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd) of ReceivedAt, inclusive. Default: 29 days before ToDate.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd), inclusive. Default: today.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>month (default), supplier or category.</summary>
    public string? GroupBy { get; set; }
}
