using System.Net.Mail;
using System.Security.Cryptography;
using FluentValidation;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Pulse.Application.Abstractions;
using Pulse.Application.Common;
using Pulse.Application.Organizations;
using Pulse.Domain.Alerts;
using Pulse.Domain.Monitors;
using Pulse.Domain.Organizations;

namespace Pulse.Application.Alerts;

public sealed class AlertChannelService(
    IAppDbContext db,
    OrgAccess access,
    ISecretProtector protector,
    IMonitorUrlGuard urlGuard,
    IAlertTransport transport,
    IOptions<AppOptions> options,
    TimeProvider clock)
{
    public const int MaxRecipients = 10;
    public const int DefaultPageSize = 30;
    public const int MaxPageSize = 100;

    /// <summary>How many test alerts one channel may send per minute, so the button cannot be used to flood a webhook.</summary>
    public const int TestsPerMinute = 5;

    public async Task<IReadOnlyList<AlertChannelDto>> ListAsync(Guid organizationId, Guid userId, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Viewer, cancellationToken);

        var channels = await db.AlertChannels.AsNoTracking()
            .Where(c => c.OrganizationId == organizationId)
            .OrderBy(c => c.Name)
            .ThenBy(c => c.CreatedAt)
            .ToListAsync(cancellationToken);
        var ids = channels.Select(c => c.Id).ToList();

        var counts = await db.MonitorAlertChannels.AsNoTracking()
            .Where(l => ids.Contains(l.ChannelId))
            .GroupBy(l => l.ChannelId)
            .Select(g => new { ChannelId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ChannelId, x => x.Count, cancellationToken);

        var latest = await db.AlertDeliveries.AsNoTracking()
            .Where(d => ids.Contains(d.ChannelId))
            .GroupBy(d => d.ChannelId)
            .Select(g => g.OrderByDescending(d => d.CreatedAt).First())
            .ToListAsync(cancellationToken);
        var latestByChannel = latest.ToDictionary(d => d.ChannelId);

        return channels.Select(channel => ToDto(
            channel,
            counts.GetValueOrDefault(channel.Id),
            latestByChannel.TryGetValue(channel.Id, out var delivery) ? ToDto(delivery, channel) : null)).ToList();
    }

    public async Task<AlertChannelCreatedDto> CreateAsync(Guid organizationId, Guid userId, AlertChannelInput input, CancellationToken cancellationToken)
    {
        await access.RequireAsync(organizationId, userId, Role.Admin, cancellationToken);
        new AlertChannelInputValidator().Check(input);

        var existing = await db.AlertChannels.CountAsync(c => c.OrganizationId == organizationId, cancellationToken);
        if (existing >= AlertChannel.MaxPerOrganization)
        {
            throw AppException.Conflict("channel_limit_reached", $"A team can have {AlertChannel.MaxPerOrganization} alert channels. Delete one to add another.");
        }

        var url = input.Type == AlertChannelType.Email ? null : await CheckUrlAsync(input, cancellationToken);
        string? secret = input.Type == AlertChannelType.Webhook ? NewSecret() : null;
        var config = new AlertConfig(input.Type == AlertChannelType.Email ? NormalizeRecipients(input.Recipients) : null, url, secret);

        var channel = new AlertChannel(organizationId, input.Type, input.Name.Trim(), protector.Protect(AlertJson.Serialize(config)), clock.GetUtcNow());
        db.AlertChannels.Add(channel);
        await db.SaveChangesAsync(cancellationToken);

        return new AlertChannelCreatedDto(ToDto(channel, 0, null), secret);
    }

    public async Task<AlertChannelDto> UpdateAsync(Guid channelId, Guid userId, AlertChannelInput input, CancellationToken cancellationToken)
    {
        var channel = await LoadAsync(channelId, userId, Role.Admin, track: true, cancellationToken);
        if (input.Type != channel.Type)
        {
            throw new AppException(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]> { ["type"] = ["A channel's type can't be changed. Create a new channel instead."] });
        }

        var current = ReadConfig(channel);
        new AlertChannelInputValidator(requireUrl: false).Check(input);

        var config = channel.Type switch
        {
            AlertChannelType.Email => current with { Recipients = NormalizeRecipients(input.Recipients) },
            _ when string.IsNullOrWhiteSpace(input.Url) => current,
            _ => current with { Url = await CheckUrlAsync(input, cancellationToken) },
        };

        channel.Name = input.Name.Trim();
        channel.ProtectedConfig = protector.Protect(AlertJson.Serialize(config));
        await db.SaveChangesAsync(cancellationToken);

        return ToDto(channel, await CountMonitorsAsync(channel.Id, cancellationToken), null);
    }

    /// <summary>Issues a new signing secret for a generic webhook. The old one stops matching right away.</summary>
    public async Task<AlertChannelCreatedDto> RotateSecretAsync(Guid channelId, Guid userId, CancellationToken cancellationToken)
    {
        var channel = await LoadAsync(channelId, userId, Role.Admin, track: true, cancellationToken);
        if (channel.Type != AlertChannelType.Webhook)
        {
            throw AppException.Invalid("no_signing_secret", "Only webhook channels have a signing secret.");
        }

        var secret = NewSecret();
        channel.ProtectedConfig = protector.Protect(AlertJson.Serialize(ReadConfig(channel) with { Secret = secret }));
        await db.SaveChangesAsync(cancellationToken);

        return new AlertChannelCreatedDto(ToDto(channel, await CountMonitorsAsync(channel.Id, cancellationToken), null), secret);
    }

    public async Task DeleteAsync(Guid channelId, Guid userId, CancellationToken cancellationToken)
    {
        var channel = await LoadAsync(channelId, userId, Role.Admin, track: true, cancellationToken);

        // Monitor links and the delivery history go with it through the database's cascades.
        db.AlertChannels.Remove(channel);
        await db.SaveChangesAsync(cancellationToken);
    }

    /// <summary>Sends a sample alert right now and reports how it went, so a wrong URL is found while setting up, not at 3 a.m.</summary>
    public async Task<DeliveryDto> TestAsync(Guid channelId, Guid userId, CancellationToken cancellationToken)
    {
        var channel = await LoadAsync(channelId, userId, Role.Admin, track: false, cancellationToken);
        var now = clock.GetUtcNow();

        var recent = await db.AlertDeliveries.CountAsync(
            d => d.ChannelId == channelId && d.Kind == AlertKind.Test && d.CreatedAt > now.AddMinutes(-1), cancellationToken);
        if (recent >= TestsPerMinute)
        {
            throw new AppException(429, "too_many_tests", "Wait a minute before sending more test alerts.");
        }

        var organizationName = await db.Organizations.Where(o => o.Id == channel.OrganizationId).Select(o => o.Name).FirstAsync(cancellationToken);
        var payload = new AlertPayload(
            AlertKind.Test,
            null,
            "Test alert from Pulse",
            null,
            null,
            "Example monitor",
            "https://example.com",
            "This is a test. Nothing is wrong.",
            now,
            null,
            organizationName,
            options.Value.PublicUrl.TrimEnd('/') + "/alerts");

        var delivery = new AlertDelivery(channel.OrganizationId, channel.Id, null, AlertKind.Test, AlertJson.Serialize(payload), now);
        db.AlertDeliveries.Add(delivery);

        try
        {
            await transport.SendAsync(channel.Type, ReadConfig(channel), payload, delivery.Id, cancellationToken);
            delivery.MarkSent(clock.GetUtcNow());
        }
        catch (AlertSendException ex)
        {
            delivery.MarkAttemptFailed(ex.Message, willRetry: false);
        }

        await db.SaveChangesAsync(cancellationToken);
        return ToDto(delivery, channel);
    }

    public async Task<DeliveryPageDto> DeliveriesAsync(Guid channelId, Guid userId, DateTimeOffset? before, int? limit, CancellationToken cancellationToken)
    {
        var channel = await LoadAsync(channelId, userId, Role.Viewer, track: false, cancellationToken);
        var size = Math.Clamp(limit ?? DefaultPageSize, 1, MaxPageSize);

        var query = db.AlertDeliveries.AsNoTracking().Where(d => d.ChannelId == channelId);
        if (before is not null)
        {
            query = query.Where(d => d.CreatedAt < before);
        }

        var rows = await query.OrderByDescending(d => d.CreatedAt).Take(size).ToListAsync(cancellationToken);
        var items = rows.Select(d => ToDto(d, channel)).ToList();

        return new DeliveryPageDto(items, items.Count == size ? items[^1].CreatedAt : null);
    }

    /// <summary>Opens a channel's stored settings. Used by the dispatcher, which has already found the delivery.</summary>
    public AlertConfig ReadConfig(AlertChannel channel) => AlertJson.Deserialize<AlertConfig>(protector.Unprotect(channel.ProtectedConfig));

    private async Task<AlertChannel> LoadAsync(Guid channelId, Guid userId, Role minimum, bool track, CancellationToken cancellationToken)
    {
        var channels = track ? db.AlertChannels.AsTracking() : db.AlertChannels.AsNoTracking();
        var found = await (
                from channel in channels
                join membership in db.Memberships on channel.OrganizationId equals membership.OrganizationId
                where channel.Id == channelId && membership.UserId == userId
                select new { Channel = channel, membership.Role })
            .FirstOrDefaultAsync(cancellationToken)
            ?? throw AppException.NotFound("channel_not_found", "That alert channel does not exist.");

        if (!found.Role.AtLeast(minimum))
        {
            throw AppException.Forbidden("insufficient_role", $"This needs the {minimum} role or higher.");
        }

        return found.Channel;
    }

    private async Task<string> CheckUrlAsync(AlertChannelInput input, CancellationToken cancellationToken)
    {
        var url = input.Url!.Trim();
        var uri = new Uri(url);

        // Webhook addresses are called from the server, so the same private-network rules as for monitors apply.
        var problem = await urlGuard.CheckHostAsync(uri, cancellationToken);
        if (problem is not null)
        {
            throw new AppException(400, "validation_failed", "Some fields need attention.", new Dictionary<string, string[]> { ["url"] = [problem] });
        }

        return uri.AbsoluteUri;
    }

    private Task<int> CountMonitorsAsync(Guid channelId, CancellationToken cancellationToken) =>
        db.MonitorAlertChannels.CountAsync(l => l.ChannelId == channelId, cancellationToken);

    private static string[] NormalizeRecipients(string[]? recipients) =>
        (recipients ?? []).Select(r => r.Trim().ToLowerInvariant()).Where(r => r.Length > 0).Distinct().ToArray();

    private static string NewSecret() => "whsec_" + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private AlertChannelDto ToDto(AlertChannel channel, int monitorCount, DeliveryDto? lastDelivery)
    {
        var config = ReadConfig(channel);
        string? destination = null;
        if (!string.IsNullOrEmpty(config.Url) && Uri.TryCreate(config.Url, UriKind.Absolute, out var uri))
        {
            destination = uri.Host;
        }

        return new AlertChannelDto(
            channel.Id,
            channel.OrganizationId,
            channel.Type,
            channel.Name,
            config.Recipients ?? [],
            destination,
            monitorCount,
            lastDelivery,
            channel.CreatedAt);
    }

    private static DeliveryDto ToDto(AlertDelivery d, AlertChannel? channel) => new(
        d.Id,
        d.ChannelId,
        channel?.Name,
        channel?.Type,
        d.IncidentId,
        d.Kind,
        d.Status,
        d.Attempts,
        d.CreatedAt,
        d.SentAt,
        d.LastError);

    /// <summary>Whether text looks like an email address. Shared with the validator.</summary>
    internal static bool IsEmail(string text) => MailAddress.TryCreate(text, out var parsed) && parsed.Address == text && text.Contains('.', StringComparison.Ordinal);

    internal static string UrlHostHint(AlertChannelType type) => type switch
    {
        AlertChannelType.Discord => "https://discord.com/api/webhooks/...",
        AlertChannelType.Slack => "https://hooks.slack.com/services/...",
        _ => "https://example.com/hooks/pulse",
    };
}

public sealed class AlertChannelInputValidator : AbstractValidator<AlertChannelInput>
{
    private static readonly string[] DiscordHosts = ["discord.com", "discordapp.com", "ptb.discord.com", "canary.discord.com"];

    public AlertChannelInputValidator(bool requireUrl = true)
    {
        RuleFor(x => x.Type).IsInEnum().WithMessage("Choose email, Discord, Slack or webhook");

        RuleFor(x => x.Name).Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Enter a name")
            .Must(name => name.Trim().Length <= AlertChannel.NameMaxLength).WithMessage($"Use {AlertChannel.NameMaxLength} characters or fewer");

        When(x => x.Type == AlertChannelType.Email, () =>
            RuleFor(x => x.Recipients).Custom((recipients, context) =>
            {
                var list = (recipients ?? []).Select(r => r.Trim()).Where(r => r.Length > 0).ToList();
                if (list.Count == 0)
                {
                    context.AddFailure("Enter at least one email address");
                }
                else if (list.Count > AlertChannelService.MaxRecipients)
                {
                    context.AddFailure($"Use {AlertChannelService.MaxRecipients} addresses or fewer");
                }
                else if (list.FirstOrDefault(r => !AlertChannelService.IsEmail(r)) is { } bad)
                {
                    context.AddFailure($"\"{bad}\" is not an email address");
                }
            }));

        When(x => x.Type != AlertChannelType.Email && (requireUrl || !string.IsNullOrWhiteSpace(x.Url)), () =>
            RuleFor(x => x.Url).Custom((url, context) =>
            {
                var type = context.InstanceToValidate.Type;
                if (string.IsNullOrWhiteSpace(url))
                {
                    context.AddFailure($"Enter the webhook address, like {AlertChannelService.UrlHostHint(type)}");
                    return;
                }

                var (uri, error) = MonitorUrl.Parse(url.Trim());
                if (uri is null)
                {
                    context.AddFailure(error ?? "Enter a valid address");
                    return;
                }

                switch (type)
                {
                    case AlertChannelType.Discord when uri.Scheme != Uri.UriSchemeHttps || !DiscordHosts.Contains(uri.Host, StringComparer.OrdinalIgnoreCase) || !uri.AbsolutePath.StartsWith("/api/webhooks/", StringComparison.Ordinal):
                        context.AddFailure("Use the webhook address from Discord, like https://discord.com/api/webhooks/...");
                        break;
                    case AlertChannelType.Slack when uri.Scheme != Uri.UriSchemeHttps || !uri.Host.Equals("hooks.slack.com", StringComparison.OrdinalIgnoreCase) || !uri.AbsolutePath.StartsWith("/services/", StringComparison.Ordinal):
                        context.AddFailure("Use the webhook address from Slack, like https://hooks.slack.com/services/...");
                        break;
                }
            }));
    }
}
