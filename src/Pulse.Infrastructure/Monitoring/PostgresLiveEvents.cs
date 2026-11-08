using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Pulse.Application.Abstractions;
using Pulse.Infrastructure.Persistence;

namespace Pulse.Infrastructure.Monitoring;

/// <summary>
/// Publishes events with PostgreSQL's NOTIFY. Every API instance listens (see the LiveEventListener there) and
/// pushes what it hears to browsers, so the worker and the API never need to know about each other.
/// </summary>
public sealed class PostgresLiveEvents(PulseDbContext db) : ILiveEvents
{
    public const string Channel = "pulse_events";

    // NOTIFY payloads are limited to 8000 bytes, which is plenty for ids and numbers.
    private const int MaxPayloadBytes = 7500;

    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() },
    };

    public async Task PublishAsync(LiveEvent liveEvent, CancellationToken cancellationToken)
    {
        var payload = JsonSerializer.Serialize(new Envelope(liveEvent.Type, liveEvent.OrganizationId, liveEvent.Data), Json);
        if (System.Text.Encoding.UTF8.GetByteCount(payload) > MaxPayloadBytes)
        {
            throw new InvalidOperationException($"Live event {liveEvent.Type} is too large to publish.");
        }

        await db.Database.ExecuteSqlAsync($"SELECT pg_notify({Channel}, {payload})", cancellationToken);
    }

    /// <summary>What goes over the wire. Kept in one place so the listener reads exactly what this writes.</summary>
    public sealed record Envelope(string Type, Guid Org, object Data);
}
