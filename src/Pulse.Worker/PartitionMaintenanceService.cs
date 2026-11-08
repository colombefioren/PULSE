using Pulse.Infrastructure.Monitoring;

namespace Pulse.Worker;

/// <summary>
/// Makes sure the next months' check_results partitions exist and drops the ones that fell out of the retention window,
/// on start and then every few hours.
/// </summary>
public sealed class PartitionMaintenanceService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<PartitionMaintenanceService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromHours(6);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(Every, clock);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<CheckPartitions>().EnsureUpcomingAsync(clock.GetUtcNow(), stoppingToken);
                var retention = await scope.ServiceProvider.GetRequiredService<CheckRetention>().RunAsync(clock.GetUtcNow(), stoppingToken);
                if (retention.DroppedPartitions.Count > 0 || retention.HourlyRowsRemoved > 0)
                {
                    logger.LogInformation(
                        "Retention dropped {Partitions} and removed {Hourly} old hourly rows",
                        string.Join(", ", retention.DroppedPartitions),
                        retention.HourlyRowsRemoved);
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Partition maintenance failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
