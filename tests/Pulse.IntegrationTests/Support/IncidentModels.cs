using System.Net;

namespace Pulse.IntegrationTests.Support;

public sealed record DeliveryInfo(
    Guid Id,
    Guid ChannelId,
    string? ChannelName,
    string? ChannelType,
    Guid? IncidentId,
    string Kind,
    string Status,
    int Attempts,
    DateTimeOffset CreatedAt,
    DateTimeOffset? SentAt,
    string? Error);

public sealed record ChannelInfo(
    Guid Id,
    Guid OrganizationId,
    string Type,
    string Name,
    List<string> Recipients,
    string? Destination,
    int MonitorCount,
    DeliveryInfo? LastDelivery,
    DateTimeOffset CreatedAt);

public sealed record ChannelCreated(ChannelInfo Channel, string? SigningSecret);

public sealed record DeliveryPage(List<DeliveryInfo> Items, DateTimeOffset? NextBefore);

public sealed record IncidentInfo(
    Guid Id,
    Guid OrganizationId,
    Guid? MonitorId,
    string? MonitorName,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? AcknowledgedAt,
    string? AcknowledgedBy,
    DateTimeOffset? ResolvedAt,
    bool IsManual);

public sealed record UpdateInfo(Guid Id, string Kind, string Status, string Message, string? Author, DateTimeOffset CreatedAt);

public sealed record IncidentDetail(IncidentInfo Incident, List<UpdateInfo> Updates, List<DeliveryInfo> Alerts);

public sealed record IncidentPage(List<IncidentInfo> Items, DateTimeOffset? NextBefore);

public sealed record SummaryInfo(int Up, int Down, int Paused, int Pending, int OpenIncidents, double? Uptime24h, double? Uptime7d, double? Uptime30d, int? AvgResponseMs);

public static class TestIncidents
{
    public static async Task<ChannelCreated> CreateChannel(this TestUser user, object body)
    {
        var response = await user.Client.Post($"/api/orgs/{user.OrgId}/alert-channels", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await user.Client.Read<ChannelCreated>(response);
    }

    public static Task<ChannelCreated> CreateEmailChannel(this TestUser user, string name = "On-call", params string[] recipients) =>
        user.CreateChannel(new { type = "Email", name, recipients = recipients.Length == 0 ? new[] { TestUsers.NewEmail("oncall") } : recipients });

    public static async Task<IncidentDetail> CreateManualIncident(this TestUser user, string title = "Slow dashboard", string severity = "Degraded", string status = "Investigating", string message = "Looking into it.")
    {
        var response = await user.Client.Post($"/api/orgs/{user.OrgId}/incidents", new { title, severity, status, message });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await user.Client.Read<IncidentDetail>(response);
    }

    /// <summary>Opens an incident for a monitor the way the check engine does, so read and write paths can be tested without running it.</summary>
    public static async Task<Guid> OpenMonitorIncident(this PulseApi api, TestUser user, MonitorInfo monitor, string title = "API is down")
    {
        var id = Guid.CreateVersion7();
        await api.Execute(
            "insert into incidents (id, organization_id, monitor_id, title, severity, status, started_at, is_manual) values ($1, $2, $3, $4, 'MajorOutage', 'Investigating', now(), false)",
            id,
            user.OrgId,
            monitor.Id,
            title);
        await api.Execute(
            "insert into incident_updates (id, incident_id, kind, status, message, created_at) values ($1, $2, 'Opened', 'Investigating', '3 failed checks in a row.', now())",
            Guid.CreateVersion7(),
            id);
        return id;
    }

    public static async Task<IncidentDetail> GetIncident(this TestUser user, Guid id) =>
        await user.Client.Read<IncidentDetail>(await user.Client.Get($"/api/incidents/{id}"));
}
