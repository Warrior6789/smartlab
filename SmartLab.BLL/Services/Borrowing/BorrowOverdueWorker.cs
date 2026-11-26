using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Interfaces.Borrowing;

namespace SmartLab.BLL.Services.Borrowing;

/// <summary>
/// Loans become overdue at 00:00 Vietnam time, and nothing in the database changes then. So one minute after startup
/// (the server may have been down at midnight), then just after every Vietnam midnight, this asks
/// <see cref="IBorrowReminderService"/> to tell the open screens. Errors are logged and the loop keeps going.
/// </summary>
public class BorrowOverdueWorker : BackgroundService
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan VietnamUtcOffset = TimeSpan.FromHours(7);
    private static readonly TimeSpan AfterMidnight = TimeSpan.FromSeconds(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<BorrowOverdueWorker> _logger;

    public BorrowOverdueWorker(IServiceScopeFactory scopeFactory, ILogger<BorrowOverdueWorker> logger)
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
                await Task.Delay(UntilNextRun(DateTime.UtcNow), stoppingToken);
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
        }
    }

    private static TimeSpan UntilNextRun(DateTime utcNow)
    {
        var nowVietnam = utcNow.Add(VietnamUtcOffset);
        return nowVietnam.Date.AddDays(1) + AfterMidnight - nowVietnam;
    }

    private async Task RunOnceAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var reminders = scope.ServiceProvider.GetRequiredService<IBorrowReminderService>();
            await reminders.AnnounceNewlyOverdueAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Announcing overdue loans failed");
        }
    }
}
