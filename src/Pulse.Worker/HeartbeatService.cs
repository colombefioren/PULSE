namespace Pulse.Worker;

/// <summary>
/// Placeholder that proves the worker host starts and logs. The scheduler, check runners,
/// rollups and alert dispatcher replace it in the milestones that build them.
/// </summary>
public sealed class HeartbeatService(ILogger<HeartbeatService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Pulse worker started");

        using var timer = new PeriodicTimer(Interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            logger.LogInformation("Pulse worker alive");
        }
    }
}
