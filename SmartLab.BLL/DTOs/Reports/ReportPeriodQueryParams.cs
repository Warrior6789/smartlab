namespace SmartLab.BLL.DTOs.Reports;

public class ReportPeriodQueryParams
{
    /// <summary>Vietnam calendar date (yyyy-MM-dd), inclusive. Default: 29 days before ToDate.</summary>
    public DateTime? FromDate { get; set; }

    /// <summary>Vietnam calendar date (yyyy-MM-dd), inclusive. Default: today.</summary>
    public DateTime? ToDate { get; set; }

    /// <summary>Only this lab; LabStaff may only pass a lab they are assigned to.</summary>
    public Guid? RoomId { get; set; }
}
