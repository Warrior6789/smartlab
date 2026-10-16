using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Constants;
using SmartLab.BLL.Interfaces.Borrowing;
using SmartLab.DAL.Entities;
using SmartLab.DAL.UnitOfWork;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Every day from the hour in system_settings BORROW_REMINDER_HOUR (Vietnam time): "due tomorrow" to borrowers whose due date is tomorrow, and "overdue" on the 1st,
/// 3rd and 7th day after the due date, then every 7 days, while components are still out. The run of a day is claimed
/// in system_settings (BORROW_REMINDER_LAST_RUN) so a restart or a second instance never sends it twice.
/// </summary>
public class BorrowReminderService : IBorrowReminderService
{
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);
    private static readonly string[] BorrowedStatuses = { BorrowRequestStatuses.Borrowing, BorrowRequestStatuses.Overdue };

    private readonly IUnitOfWork _uow;
    private readonly IBorrowNotifier _notifier;
    private readonly ILogger<BorrowReminderService> _logger;

    public BorrowReminderService(IUnitOfWork uow, IBorrowNotifier notifier, ILogger<BorrowReminderService> logger)
    {
        _uow = uow;
        _notifier = notifier;
        _logger = logger;
    }

    public async Task<int> SendDailyRemindersAsync(CancellationToken ct = default)
    {
        var sendHour = await SystemSettingValues.GetIntAsync(_uow, SystemSettingKeys.BorrowReminderHour, ct);
        if (sendHour == null || sendHour < 0 || sendHour > 23)
        {
            _logger.LogWarning("Borrow reminders skipped: system_settings {Key} must be an hour from 0 to 23",
                SystemSettingKeys.BorrowReminderHour);
            return 0;
        }

        var nowVietnam = DateTime.UtcNow.Add(VietnamUtcOffset);
        if (nowVietnam.Hour < sendHour)
            return 0;

        var today = nowVietnam.Date;
        if (!await ClaimTodayAsync(today, ct))
            return 0;

        var dueBefore = DateTime.SpecifyKind(today.AddDays(2) - VietnamUtcOffset, DateTimeKind.Utc);
        var requests = await _uow.Repository<BorrowRequest>().QueryNoTracking()
            .Where(r => BorrowedStatuses.Contains(r.Status) && r.DueDate < dueBefore)
            .OrderBy(r => r.DueDate)
            .Select(r => new { r.BorrowRequestId, r.DueDate })
            .ToListAsync(ct);

        var sent = 0;
        foreach (var request in requests)
        {
            var dueDay = request.DueDate.Add(VietnamUtcOffset).Date;
            var daysOverdue = (today - dueDay).Days;

            if (daysOverdue == -1)
            {
                await _notifier.DueSoonAsync(request.BorrowRequestId, ct);
                sent++;
            }
            else if (IsOverdueReminderDay(daysOverdue))
            {
                await _notifier.OverdueAsync(request.BorrowRequestId, daysOverdue, ct);
                sent++;
            }
        }

        _logger.LogInformation("Borrow reminders for {Day:yyyy-MM-dd}: {Count} sent", today, sent);
        return sent;
    }

    private static bool IsOverdueReminderDay(int daysOverdue)
    {
        if (daysOverdue < 1)
            return false;
        if (daysOverdue == 1 || daysOverdue == 3)
            return true;

        return daysOverdue % 7 == 0;
    }

    /// <summary>True when this call won today's run: the setting moved to today, or the row did not exist yet.</summary>
    private async Task<bool> ClaimTodayAsync(DateTime today, CancellationToken ct)
    {
        var value = today.ToString("yyyy-MM-dd");
        var now = DateTime.UtcNow;
        var settings = _uow.Repository<SystemSetting>();

        var claimed = await settings.Query()
            .Where(s => s.SettingKey == SystemSettingKeys.BorrowReminderLastRun && s.SettingValue != value)
            .ExecuteUpdateAsync(s => s
                .SetProperty(x => x.SettingValue, value)
                .SetProperty(x => x.UpdatedAt, now), ct);
        if (claimed > 0)
            return true;

        var exists = await settings.QueryNoTracking()
            .AnyAsync(s => s.SettingKey == SystemSettingKeys.BorrowReminderLastRun, ct);
        if (exists)
            return false;

        await settings.AddAsync(new SystemSetting
        {
            SettingKey = SystemSettingKeys.BorrowReminderLastRun,
            SettingValue = value,
            Description = "Ngày (giờ Việt Nam) gần nhất đã gửi mail nhắc hạn trả",
            UpdatedAt = now,
        }, ct);
        try
        {
            await _uow.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException)
        {
            return false;
        }
    }
}
