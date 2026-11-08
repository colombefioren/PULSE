using System.Net;

namespace Pulse.IntegrationTests.Support;

public sealed record ComponentInfo(Guid Id, string Name, string? GroupName, int SortOrder, List<Guid> MonitorIds);

public sealed record StatusPageInfo(
    Guid Id,
    Guid OrganizationId,
    string Slug,
    string Title,
    string AccentColor,
    string Footer,
    bool ShowResponseTimes,
    string? LogoUrl,
    int SubscriberCount,
    List<ComponentInfo> Components,
    DateTimeOffset CreatedAt);

public sealed record PublicDay(string Date, double? UptimePct, int DowntimeSeconds, int Incidents);

public sealed record PublicComponent(Guid Id, string Name, string? Group, string Status, double? Uptime90d, int? AvgResponseMs, List<PublicDay> Days);

public sealed record PublicUpdate(string Status, string Message, DateTimeOffset At);

public sealed record PublicIncident(
    Guid Id,
    string Title,
    string Severity,
    string Status,
    DateTimeOffset StartedAt,
    DateTimeOffset? ResolvedAt,
    List<string> Components,
    List<PublicUpdate> Updates);

public sealed record PublicStatus(
    string Slug,
    string Title,
    string? LogoUrl,
    string AccentColor,
    string Footer,
    bool ShowResponseTimes,
    DateTimeOffset GeneratedAt,
    List<PublicComponent> Components,
    List<PublicIncident> ActiveIncidents,
    List<PublicIncident> History);

public static class TestStatusPages
{
    /// <summary>The smallest bytes that pass for a PNG, JPEG or WebP: the signature and some padding. Enough for the type check.</summary>
    public static byte[] Png(int size = 64) => Pad([0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A], size);

    public static byte[] Jpeg(int size = 64) => Pad([0xFF, 0xD8, 0xFF, 0xE0], size);

    public static byte[] Webp(int size = 64) => Pad([.. "RIFF"u8, 0, 0, 0, 0, .. "WEBP"u8], size);

    private static byte[] Pad(byte[] head, int size)
    {
        var bytes = new byte[Math.Max(size, head.Length + 1)];
        head.CopyTo(bytes, 0);
        return bytes;
    }

    public static string NewSlug(string prefix = "status") => $"{prefix}-{Guid.NewGuid():N}"[..32];

    public static async Task<StatusPageInfo> CreateStatusPage(this TestUser user, string? slug = null, string title = "Acme status", object? extra = null)
    {
        var body = extra ?? new { slug = slug ?? NewSlug(), title };
        var response = await user.Client.Post($"/api/orgs/{user.OrgId}/status-pages", body);
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await user.Client.Read<StatusPageInfo>(response);
    }

    public static async Task<ComponentInfo> AddComponent(this TestUser user, StatusPageInfo page, string name, params MonitorInfo[] monitors)
    {
        var response = await user.Client.Post($"/api/status-pages/{page.Id}/components", new { name, groupName = (string?)null, monitorIds = monitors.Select(m => m.Id).ToArray() });
        response.StatusCode.Should().Be(HttpStatusCode.Created, await response.Content.ReadAsStringAsync());
        return await user.Client.Read<ComponentInfo>(response);
    }

    public static async Task<StatusPageInfo> GetStatusPage(this TestUser user, Guid id) =>
        await user.Client.Read<StatusPageInfo>(await user.Client.Get($"/api/status-pages/{id}"));

    /// <summary>What a visitor gets, with a client that has no session.</summary>
    public static async Task<PublicStatus> ReadPublic(this PulseApi api, string slug)
    {
        var visitor = api.NewClient();
        return await visitor.Read<PublicStatus>(await visitor.Get($"/api/public/status/{slug}"));
    }
}
