using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Pulse.Infrastructure.Monitoring;

namespace Pulse.Api.Live;

/// <summary>
/// Listens on PostgreSQL's <c>pulse_events</c> channel and forwards each event to the SignalR group of the
/// organization it belongs to. It holds one dedicated connection and reconnects with a growing pause if it drops.
/// </summary>
public sealed class LiveEventListener(IConfiguration configuration, IHubContext<LiveHub> hub, ILogger<LiveEventListener> logger) : BackgroundService
{
    private static readonly TimeSpan MaxPause = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var connectionString = new NpgsqlConnectionStringBuilder(configuration.GetConnectionString("Default"))
        {
            Pooling = false,
            KeepAlive = 30,
        }.ConnectionString;

        var pause = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var connection = new NpgsqlConnection(connectionString);
                await connection.OpenAsync(stoppingToken);
                connection.Notification += (_, e) => _ = ForwardAsync(e.Payload, stoppingToken);

                await using (var listen = new NpgsqlCommand($"LISTEN {PostgresLiveEvents.Channel}", connection))
                {
                    await listen.ExecuteNonQueryAsync(stoppingToken);
                }

                logger.LogInformation("Listening for live events");
                pause = TimeSpan.FromSeconds(1);

                while (!stoppingToken.IsCancellationRequested)
                {
                    await connection.WaitAsync(stoppingToken);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Live event listener lost its connection, retrying in {Pause}", pause);
                await Task.Delay(pause, stoppingToken);
                pause = TimeSpan.FromSeconds(Math.Min(pause.TotalSeconds * 2, MaxPause.TotalSeconds));
            }
        }
    }

    private async Task ForwardAsync(string payload, CancellationToken cancellationToken)
    {
        try
        {
            using var document = JsonDocument.Parse(payload);
            var root = document.RootElement;
            var type = root.GetProperty("type").GetString()!;
            var organizationId = root.GetProperty("org").GetGuid();

            await hub.Clients.Group(LiveHub.GroupFor(organizationId)).SendAsync(type, root.GetProperty("data").Clone(), cancellationToken);
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex, "Ignored a live event it could not read");
        }
    }
}
