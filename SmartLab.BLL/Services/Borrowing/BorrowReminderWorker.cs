using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Interfaces.Borrowing;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Background loop: one minute after startup, then every 30 minutes, asks <see cref="IBorrowReminderService"/> to run
/// today's reminders (it runs them only once per day). Errors are logged and the loop keeps going.
/// </summary>
public class BorrowReminderWorker : BackgroundService
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(30);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BorrowReminderWorker> _logger;

    public BorrowReminderWorker(IServiceScopeFactory scopeFactory, ILogger<BorrowReminderWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await Task.Delay(FirstDelay, stoppingToken);
            while (!stoppingToken.IsCancellationRequested)
            {
                await RunOnceAsync(stoppingToken);
                await Task.Delay(Interval, stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reminders = scope.ServiceProvider.GetRequiredService<IBorrowReminderService>();
            await reminders.SendDailyRemindersAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Borrow reminder run failed");
        }
    }
}
