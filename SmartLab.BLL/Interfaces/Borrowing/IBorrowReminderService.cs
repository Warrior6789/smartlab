namespace SmartLab.BLL.Interfaces.Borrowing;

/// <summary>Daily reminders to borrowers whose components are due tomorrow or overdue.</summary>
public interface IBorrowReminderService
{
    /// <summary>
    /// Runs the reminders of today (Vietnam time) once: does nothing before the send hour or when today already ran,
    /// even with several app instances. Returns how many reminders were sent.
    /// </summary>
    Task<int> SendDailyRemindersAsync(CancellationToken ct = default);
}
