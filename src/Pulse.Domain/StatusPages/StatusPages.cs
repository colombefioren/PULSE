using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Pulse.Domain.Common;

namespace Pulse.Domain.StatusPages;

/// <summary>A public page that shows how a team's services are doing. Anyone can read it; only members can change it.</summary>
public sealed partial class StatusPage
{
    public const int MaxPerOrganization = 5;
    public const int TitleMaxLength = 80;
    public const int FooterMaxLength = 300;
    public const int SlugMinLength = 3;
    public const int SlugMaxLength = 40;
    public const int LogoMaxBytes = 256 * 1024;
    public const string DefaultAccent = "#fac303";

    /// <summary>Raster formats only. SVG can carry script, and the logo is served from our own origin.</summary>
    public static readonly string[] LogoTypes = ["image/png", "image/jpeg", "image/webp"];

    /// <summary>Words that would collide with routes or look official.</summary>
    private static readonly HashSet<string> Reserved = new(StringComparer.Ordinal)
    {
        "api", "admin", "app", "login", "signup", "status", "demo", "dashboard", "monitors", "incidents", "alerts",
        "settings", "static", "assets", "health", "badge", "public", "www", "pulse",
    };

    private StatusPage()
    {
    }

    public StatusPage(Guid organizationId, string slug, string title, DateTimeOffset now)
    {
        Id = Ids.NewAt(now);
        OrganizationId = organizationId;
        Slug = slug;
        Title = title;
        AccentColor = DefaultAccent;
        Footer = string.Empty;
        ShowResponseTimes = true;
        CreatedAt = now;
        UpdatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid OrganizationId { get; private set; }

    public string Slug { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>Six-digit hex like <c>#fac303</c>.</summary>
    public string AccentColor { get; set; } = DefaultAccent;

    public string Footer { get; set; } = string.Empty;

    public bool ShowResponseTimes { get; set; }

    public string? LogoContentType { get; private set; }

    public byte[]? LogoData { get; private set; }

    /// <summary>Changes with each upload so browsers fetch the new image instead of the cached one.</summary>
    public int LogoVersion { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; set; }

    public bool HasLogo => LogoData is { Length: > 0 };

    public void SetLogo(string contentType, byte[] data)
    {
        LogoContentType = contentType;
        LogoData = data;
        LogoVersion++;
    }

    public void ClearLogo()
    {
        LogoContentType = null;
        LogoData = null;
        LogoVersion++;
    }

    public static bool IsValidSlug(string slug) =>
        slug.Length is >= SlugMinLength and <= SlugMaxLength && SlugPattern().IsMatch(slug) && !Reserved.Contains(slug);

    public static bool IsValidAccent(string color) => AccentPattern().IsMatch(color);

    /// <summary>Which of the logo's first bytes say it really is the image type claimed.</summary>
    public static bool LooksLike(string contentType, ReadOnlySpan<byte> data) => contentType switch
    {
        "image/png" => data.Length > 8 && data[..8].SequenceEqual(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }),
        "image/jpeg" => data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF,
        "image/webp" => data.Length > 12 && data[..4].SequenceEqual("RIFF"u8) && data[8..12].SequenceEqual("WEBP"u8),
        _ => false,
    };

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9-]*[a-z0-9])$")]
    private static partial Regex SlugPattern();

    [GeneratedRegex("^#[0-9a-fA-F]{6}$")]
    private static partial Regex AccentPattern();
}

/// <summary>One line on a status page, such as "API" or "Checkout", fed by one or more monitors.</summary>
public sealed class StatusComponent
{
    public const int NameMaxLength = 60;
    public const int GroupMaxLength = 40;
    public const int MaxPerPage = 30;

    private StatusComponent()
    {
    }

    public StatusComponent(Guid statusPageId, string name, string? groupName, int sortOrder, DateTimeOffset now)
    {
        Id = Ids.NewAt(now);
        StatusPageId = statusPageId;
        Name = name;
        GroupName = groupName;
        SortOrder = sortOrder;
    }

    public Guid Id { get; private set; }

    public Guid StatusPageId { get; private set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>The heading components are grouped under, or null for none.</summary>
    public string? GroupName { get; set; }

    public int SortOrder { get; set; }
}

/// <summary>A monitor that feeds a component.</summary>
public sealed class ComponentMonitor
{
    private ComponentMonitor()
    {
    }

    public ComponentMonitor(Guid componentId, Guid monitorId)
    {
        ComponentId = componentId;
        MonitorId = monitorId;
    }

    public Guid ComponentId { get; private set; }

    public Guid MonitorId { get; private set; }
}

/// <summary>The components a manually posted incident affects.</summary>
public sealed class IncidentComponent
{
    private IncidentComponent()
    {
    }

    public IncidentComponent(Guid incidentId, Guid componentId)
    {
        IncidentId = incidentId;
        ComponentId = componentId;
    }

    public Guid IncidentId { get; private set; }

    public Guid ComponentId { get; private set; }
}

/// <summary>Someone who asked for emails about a status page's incidents. Counts only once they have confirmed.</summary>
public sealed class Subscriber
{
    public const int MaxPerPage = 5000;

    private Subscriber()
    {
    }

    public Subscriber(Guid statusPageId, string email, DateTimeOffset now)
    {
        Id = Ids.NewAt(now);
        StatusPageId = statusPageId;
        Email = email;
        ConfirmToken = NewToken();
        UnsubscribeToken = NewToken();
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid StatusPageId { get; private set; }

    /// <summary>Lower case, so the same address cannot subscribe twice.</summary>
    public string Email { get; private set; } = string.Empty;

    public DateTimeOffset? ConfirmedAt { get; private set; }

    /// <summary>Sent in the confirmation email. Cleared once used.</summary>
    public string? ConfirmToken { get; private set; }

    /// <summary>In every email, so one click ends the subscription.</summary>
    public string UnsubscribeToken { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsConfirmed => ConfirmedAt is not null;

    public void Confirm(DateTimeOffset now)
    {
        ConfirmedAt ??= now;
        ConfirmToken = null;
    }

    /// <summary>A fresh link for someone who subscribed again before confirming.</summary>
    public void RenewConfirmation() => ConfirmToken = NewToken();

    public static string NewToken() => Base64Url(RandomNumberGenerator.GetBytes(24));

    private static string Base64Url(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
