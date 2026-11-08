using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using Npgsql;
using Pulse.Api.Public;
using Pulse.Application.StatusPages;
using Pulse.Infrastructure.Monitoring;

namespace Pulse.Api.Live;

/// <summary>
/// Listens on PostgreSQL's <c>pulse_events</c> channel and forwards each event to the SignalR group of the
/// organization it belongs to. It holds one dedicated connection and reconnects with a growing pause if it drops.
/// </summary>
public sealed class LiveEventListener(
    IConfiguration configuration,
    IHubContext<LiveHub> hub,
    IHubContext<StatusHub> statusHub,
    IServiceScopeFactory scopes,
    StatusCache statusCache,
    ILogger<LiveEventListener> logger) : BackgroundService
{
    /// <summary>The events that can change what a public status page shows. A single check result cannot: the page is rebuilt from the database at most every 30 seconds anyway.</summary>
    private static readonly HashSet<string> PublicStatusEvents =
        ["MonitorStatusChanged", "MonitorChanged", "IncidentOpened", "IncidentUpdated", "IncidentResolved", "StatusPageChanged"];

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

            var data = root.GetProperty("data").Clone();
            await hub.Clients.Group(LiveHub.GroupFor(organizationId)).SendAsync(type, data, cancellationToken);

            if (PublicStatusEvents.Contains(type))
            {
                await RefreshPublicPagesAsync(organizationId, data, cancellationToken);
            }
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        {
            logger.LogWarning(ex, "Ignored a live event it could not read");
        }
    }

    /// <summary>Drops the cached copy of the team's public pages and tells everyone looking at one to fetch it again.</summary>
    private async Task RefreshPublicPagesAsync(Guid organizationId, JsonElement data, CancellationToken cancellationToken)
    {
        try
        {
            // A page that was renamed or deleted is no longer in the list, so its old address is evicted from the event itself.
            foreach (var property in new[] { "slug", "previousSlug" })
            {
                if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty(property, out var value) && value.GetString() is { } slug)
                {
                    statusCache.Evict(slug);
                }
            }

            await using var scope = scopes.CreateAsyncScope();
            foreach (var (id, slug) in await scope.ServiceProvider.GetRequiredService<PublicStatusService>().PagesOfOrganizationAsync(organizationId, cancellationToken))
            {
                statusCache.Evict(slug);
                await statusHub.Clients.Group(StatusHub.GroupFor(id)).SendAsync("StatusChanged", cancellationToken);
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not refresh public status pages after a live event");
        }
    }
}
