using Pulse.Infrastructure.Monitoring;

namespace Pulse.Worker;

/// <summary>Makes sure the next months' check_results partitions exist, on start and then every few hours.</summary>
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
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Could not create the upcoming check partitions");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
