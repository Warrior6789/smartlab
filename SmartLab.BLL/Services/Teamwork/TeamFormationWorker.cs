using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using SmartLab.BLL.Interfaces.Teamwork;

namespace SmartLab.BLL.Services.Teamwork;

/// <summary>
/// Background loop: one minute after startup, then every 10 minutes, handles team formation deadlines. Errors are logged and
/// the loop keeps going.
/// </summary>
public class TeamFormationWorker : BackgroundService
{
    private static readonly TimeSpan FirstDelay = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<TeamFormationWorker> _logger;

    public TeamFormationWorker(IServiceScopeFactory scopeFactory, ILogger<TeamFormationWorker> logger)
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
            var deadlines = scope.ServiceProvider.GetRequiredService<ITeamFormationDeadlineService>();
            await deadlines.RunAsync(ct);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(ex, "Team formation deadline run failed");
        }
    }
}
