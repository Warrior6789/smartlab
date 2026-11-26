namespace SmartLab.BLL.Services.Borrowing;

/// <summary>Vietnam calendar days stored as the UTC time of their start, like due_date.</summary>
internal static class VietnamDay
{
    private static readonly TimeSpan UtcOffset = TimeSpan.FromHours(7);

    /// <summary>
    /// Start of the Vietnam calendar day, in UTC. A plain date ("2026-10-28") is that day. A value with "Z" or an offset
    /// (and DateTime.UtcNow) is first turned into Vietnam time, so the server time zone never shifts the day.
    /// </summary>
    public static DateTime StartUtc(DateTime value)
    {
        var date = value.Date;
        if (value.Kind != DateTimeKind.Unspecified)
            date = (value.ToUniversalTime() + UtcOffset).Date;

        return DateTime.SpecifyKind(date - UtcOffset, DateTimeKind.Utc);
    }

    public static string Text(DateTime utc) => utc.Add(UtcOffset).ToString("dd/MM/yyyy");
}
