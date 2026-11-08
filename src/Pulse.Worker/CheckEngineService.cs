using Pulse.Infrastructure.Checks;

namespace Pulse.Worker;

/// <summary>Hosts the check engine for the lifetime of the worker.</summary>
public sealed class CheckEngineService(CheckEngine engine, ILogger<CheckEngineService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Check engine started");
        await engine.RunAsync(stoppingToken);
        logger.LogInformation("Check engine stopped");
    }
}
