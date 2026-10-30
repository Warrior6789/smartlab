using SmartLab.BLL.Common.Exceptions;

namespace SmartLab.BLL.Services.Reports;

/// <summary>A report period of whole Vietnam calendar days, with its UTC bounds for querying timestamptz columns.</summary>
public class ReportPeriod
{
    /// <summary>Vietnam has no DST, so a fixed UTC+7 offset is exact.</summary>
    public static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);

    private const int DefaultDays = 30;
    private const int MaxDays = 366;

    public DateTime FromDate { get; private init; }
    public DateTime ToDate { get; private init; }
    public DateTime FromUtc { get; private init; }
    public DateTime ToExclusiveUtc { get; private init; }

    /// <summary>Missing ToDate = today, missing FromDate = 30 days ending at ToDate.</summary>
    public static ReportPeriod Create(DateTime? fromDate, DateTime? toDate)
    {
        var to = VietnamToday();
        if (toDate.HasValue)
            to = toDate.Value.Date;

        var from = to.AddDays(1 - DefaultDays);
        if (fromDate.HasValue)
            from = fromDate.Value.Date;

        if (from > to)
            throw new BadRequestException("Từ ngày phải trước hoặc bằng đến ngày");

        if ((to - from).TotalDays + 1 > MaxDays)
            throw new BadRequestException($"Khoảng thời gian tối đa {MaxDays} ngày");

        return new ReportPeriod
        {
            FromDate = from,
            ToDate = to,
            FromUtc = StartOfVietnamDay(from),
            ToExclusiveUtc = StartOfVietnamDay(to.AddDays(1)),
        };
    }

    public static DateTime VietnamToday() => (DateTime.UtcNow + VietnamUtcOffset).Date;

    public static DateTime StartOfVietnamDay(DateTime date) =>
        DateTime.SpecifyKind(date.Date - VietnamUtcOffset, DateTimeKind.Utc);
}
