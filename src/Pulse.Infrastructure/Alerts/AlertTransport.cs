using System.Net;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Pulse.Application.Abstractions;
using Pulse.Application.Alerts;
using Pulse.Domain.Alerts;
using Pulse.Infrastructure.Checks;

namespace Pulse.Infrastructure.Alerts;

/// <summary>
/// The HTTP client used for webhooks. Like the check engine's, it connects only to public addresses (checked at
/// connect time), uses no proxy and follows no redirects: a webhook that answers with a redirect is a mistake or
/// an attempt to bounce the request somewhere else.
/// </summary>
public sealed class AlertHttp : IDisposable
{
    public const string UserAgent = "Pulse-Alerts/1.0 (+https://github.com/colombefioren/PULSE)";

    public AlertHttp(IAddressPolicy policy)
    {
        Client = new HttpClient(
            new SocketsHttpHandler
            {
                UseProxy = false,
                UseCookies = false,
                AllowAutoRedirect = false,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectCallback = (context, token) => GuardedConnect.ConnectAsync(policy, context, token),
            },
            disposeHandler: true)
        {
            Timeout = TimeSpan.FromSeconds(10),
        };
        Client.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
    }

    public HttpClient Client { get; }

    public void Dispose() => Client.Dispose();
}

public sealed class AlertTransport(IEmailSender email, AlertHttp http, TimeProvider clock) : IAlertTransport
{
    public async Task SendAsync(AlertChannelType type, AlertConfig config, AlertPayload payload, Guid deliveryId, CancellationToken cancellationToken)
    {
        switch (type)
        {
            case AlertChannelType.Email:
                await SendEmailAsync(config, payload, cancellationToken);
                break;
            case AlertChannelType.Slack:
                await PostAsync(config.Url, AlertMessages.Slack(payload), null, null, cancellationToken);
                break;
            case AlertChannelType.Discord:
                await PostAsync(config.Url, AlertMessages.Discord(payload), null, null, cancellationToken);
                break;
            case AlertChannelType.Webhook:
                await PostAsync(config.Url, AlertMessages.Webhook(payload, deliveryId, clock.GetUtcNow()), config.Secret, payload, cancellationToken, deliveryId);
                break;
            default:
                throw new AlertSendException("This kind of channel can't send alerts.");
        }
    }

    private async Task SendEmailAsync(AlertConfig config, AlertPayload payload, CancellationToken cancellationToken)
    {
        var recipients = config.Recipients ?? [];
        if (recipients.Length == 0)
        {
            throw new AlertSendException("The channel has no email addresses.");
        }

        var failed = new List<string>();
        foreach (var recipient in recipients)
        {
            try
            {
                await email.SendAsync(AlertMessages.Email(payload, recipient), cancellationToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed.Add(recipient);
            }
        }

        if (failed.Count > 0)
        {
            // Resending to the addresses that did work would be a duplicate, but the delivery is one unit, so the whole thing is retried.
            throw new AlertSendException($"Could not send the email to {string.Join(", ", failed)}. Check that the mail server is reachable.");
        }
    }

    private async Task PostAsync(string? url, object body, string? secret, AlertPayload? signedFor, CancellationToken cancellationToken, Guid? deliveryId = null)
    {
        if (string.IsNullOrEmpty(url))
        {
            throw new AlertSendException("The channel has no webhook address.");
        }

        var uri = new Uri(url);
        var bytes = Encoding.UTF8.GetBytes(AlertJson.Serialize(body));

        using var request = new HttpRequestMessage(HttpMethod.Post, uri) { Content = new ByteArrayContent(bytes) };
        request.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json") { CharSet = "utf-8" };
        if (secret is not null && signedFor is not null)
        {
            request.Headers.TryAddWithoutValidation("X-Pulse-Signature", AlertMessages.Sign(secret, bytes));
            request.Headers.TryAddWithoutValidation("X-Pulse-Event", signedFor.Kind switch { AlertKind.IncidentOpened => "incident.opened", AlertKind.IncidentResolved => "incident.resolved", _ => "test" });
            request.Headers.TryAddWithoutValidation("X-Pulse-Delivery", deliveryId?.ToString());
        }

        try
        {
            using var response = await http.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new AlertSendException($"{uri.Host} answered {(int)response.StatusCode} {response.ReasonPhrase}".TrimEnd());
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            throw new AlertSendException($"{uri.Host} did not answer within 10 seconds.");
        }
        catch (HttpRequestException ex)
        {
            throw new AlertSendException(Describe(ex, uri));
        }
    }

    private static string Describe(HttpRequestException error, Uri uri)
    {
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case BlockedAddressException:
                    return $"Pulse will not call {uri.Host}: it points at a private or internal address.";
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData }:
                    return $"{uri.Host} could not be resolved.";
                case SocketException { SocketErrorCode: SocketError.ConnectionRefused }:
                    return $"{uri.Host} refused the connection.";
                case AuthenticationException auth:
                    return $"TLS handshake with {uri.Host} failed: {auth.Message}";
            }
        }

        return $"Could not reach {uri.Host}: {error.Message}";
    }
}
