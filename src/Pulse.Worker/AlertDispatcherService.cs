using Pulse.Infrastructure.Alerts;

namespace Pulse.Worker;

/// <summary>Hosts the outbox dispatcher, which sends alerts, for the lifetime of the worker.</summary>
public sealed class AlertDispatcherService(OutboxDispatcher dispatcher, ILogger<AlertDispatcherService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("Alert dispatcher started");
        await dispatcher.RunAsync(stoppingToken);
        logger.LogInformation("Alert dispatcher stopped");
    }
}
