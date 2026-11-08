using System.Net;
using System.Net.Sockets;

namespace Pulse.Infrastructure.Checks;

/// <summary>
/// The connect step shared by everything that calls out to an address a user supplied (monitor checks and
/// alert webhooks). It resolves the name itself and refuses to connect if any address it resolves to is off
/// limits, so the guard applies to the address actually used, on every connection, redirects included.
/// </summary>
public static class GuardedConnect
{
    public static async ValueTask<Stream> ConnectAsync(IAddressPolicy policy, SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        // One private record among public ones is enough to refuse: a name must not be able to steer the connection inward.
        if (addresses.Any(address => !policy.IsAllowed(address)))
        {
            throw new BlockedAddressException(host);
        }

        SocketException? last = null;
        foreach (var address in addresses)
        {
            var socket = new Socket(address.AddressFamily, SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(new IPEndPoint(address, context.DnsEndPoint.Port), cancellationToken);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch (SocketException ex)
            {
                socket.Dispose();
                last = ex;
            }
        }

        throw last!;
    }
}
