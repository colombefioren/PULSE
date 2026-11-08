using Pulse.Infrastructure.Monitoring;

namespace Pulse.Worker;

/// <summary>
/// Keeps the hourly and daily totals current. On start it recomputes the last 35 days (a restart after downtime
/// heals itself), then it refreshes the last few hours and yesterday and today every 10 minutes.
/// </summary>
public sealed class RollupService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RollupService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(10);
    private const int BackfillDays = 35;
    private const int RecentHours = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var first = true;
        using var timer = new PeriodicTimer(Every, clock);
        do
        {
            try
            {
                var now = clock.GetUtcNow();
                var today = DateOnly.FromDateTime(now.UtcDateTime);
                await using var scope = scopes.CreateAsyncScope();
                var days = await scope.ServiceProvider.GetRequiredService<DailyRollup>().RunAsync(today.AddDays(first ? -BackfillDays : -1), today, stoppingToken);
                var hours = await scope.ServiceProvider.GetRequiredService<HourlyRollup>().RunAsync(first ? now.AddDays(-BackfillDays) : now.AddHours(-RecentHours), now, stoppingToken);
                logger.LogInformation("Rollups refreshed: {Days} daily and {Hours} hourly rows", days, hours);
                first = false;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Daily rollup failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
