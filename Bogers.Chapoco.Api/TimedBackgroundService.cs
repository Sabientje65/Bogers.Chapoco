namespace Bogers.Chapoco.Api;

/// <summary>
/// Baseclass for background services running on an interval
/// </summary>
public abstract class TimedBackgroundService : BackgroundService
{
    protected abstract TimeSpan Interval { get; }
    
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Interval);

        await Run(stoppingToken);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await Run(stoppingToken);
        }
    }

    protected abstract Task Run(CancellationToken stoppingToken);
}