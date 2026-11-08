using System.Net;
using System.Net.Sockets;
using Pulse.Application.Abstractions;
using Pulse.Domain.Monitors;

namespace Pulse.Infrastructure.Monitoring;

/// <summary>
/// Looks the host up when a monitor is saved, so an obviously internal name is refused straight away with a
/// clear message. A name that does not resolve yet is accepted: a monitor for a site that is down is still valid.
/// Every check repeats the test against the address it actually connects to, because DNS can change later.
/// </summary>
public sealed class DnsUrlGuard(bool allowPrivate = false) : IMonitorUrlGuard
{
    private static readonly TimeSpan LookupTimeout = TimeSpan.FromSeconds(3);

    public async Task<string?> CheckHostAsync(Uri uri, CancellationToken cancellationToken)
    {
        if (allowPrivate || IPAddress.TryParse(uri.Host.Trim('[', ']'), out _))
        {
            return null; // already judged by MonitorUrl
        }

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(LookupTimeout);

        try
        {
            var addresses = await Dns.GetHostAddressesAsync(uri.IdnHost, timeout.Token);
            return addresses.Any(address => !NetworkPolicy.IsPublic(address))
                ? $"{uri.IdnHost} points at a private or internal address, which Pulse cannot reach."
                : null;
        }
        catch (SocketException)
        {
            return null;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return null;
        }
    }
}
