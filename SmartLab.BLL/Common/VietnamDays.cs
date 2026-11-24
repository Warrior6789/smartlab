namespace SmartLab.BLL.Common;

/// <summary>UTC bounds of Vietnam calendar days (UTC+7, no DST), for filtering timestamptz columns by date.</summary>
public static class VietnamDays
{
    private static readonly TimeSpan UtcOffset = TimeSpan.FromHours(7);
    private static readonly DateTime MinDay = new(2000, 1, 1);
    private static readonly DateTime MaxDay = new(2100, 12, 31);

    public static DateTime StartUtc(DateTime day) =>
        DateTime.SpecifyKind(Clamp(day) - UtcOffset, DateTimeKind.Utc);

    public static DateTime EndExclusiveUtc(DateTime day) =>
        DateTime.SpecifyKind(Clamp(day).AddDays(1) - UtcOffset, DateTimeKind.Utc);

    /// <summary>Keeps a typed date inside 2000-2100: no data lies outside, and extreme years overflow date arithmetic.</summary>
    public static DateTime Clamp(DateTime day)
    {
        if (day.Date < MinDay)
            return MinDay;
        if (day.Date > MaxDay)
            return MaxDay;

        return day.Date;
    }
}
