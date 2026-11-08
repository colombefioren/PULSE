using Pulse.Infrastructure.Monitoring;

namespace Pulse.Worker;

/// <summary>
/// Keeps the daily uptime totals current. On start it recomputes the last 35 days (a restart after downtime
/// heals itself), then it refreshes yesterday and today every 10 minutes.
/// </summary>
public sealed class RollupService(IServiceScopeFactory scopes, TimeProvider clock, ILogger<RollupService> logger) : BackgroundService
{
    private static readonly TimeSpan Every = TimeSpan.FromMinutes(10);
    private const int BackfillDays = 35;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var first = true;
        using var timer = new PeriodicTimer(Every, clock);
        do
        {
            try
            {
                var today = DateOnly.FromDateTime(clock.GetUtcNow().UtcDateTime);
                await using var scope = scopes.CreateAsyncScope();
                var rows = await scope.ServiceProvider.GetRequiredService<DailyRollup>().RunAsync(today.AddDays(first ? -BackfillDays : -1), today, stoppingToken);
                logger.LogInformation("Daily rollup refreshed {Rows} rows", rows);
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
