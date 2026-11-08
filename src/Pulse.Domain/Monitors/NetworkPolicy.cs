using System.Net;
using System.Net.Sockets;

namespace Pulse.Domain.Monitors;

/// <summary>
/// Decides which addresses a monitor may contact. Anything that is not a normal public internet address is
/// refused, so a monitor can never be used to probe the host, the private network or a cloud metadata service.
/// </summary>
public static class NetworkPolicy
{
    public static bool IsPublic(IPAddress address)
    {
        if (address.AddressFamily == AddressFamily.InterNetworkV6)
        {
            return IsPublicV6(address);
        }

        return address.AddressFamily == AddressFamily.InterNetwork && IsPublicV4(address.GetAddressBytes());
    }

    private static bool IsPublicV4(ReadOnlySpan<byte> b)
    {
        return !(
            b[0] == 0 // "this network"
            || b[0] == 10 // private
            || (b[0] == 100 && b[1] is >= 64 and <= 127) // carrier-grade NAT
            || b[0] == 127 // loopback
            || (b[0] == 169 && b[1] == 254) // link-local, including 169.254.169.254 (cloud metadata)
            || (b[0] == 172 && b[1] is >= 16 and <= 31) // private
            || (b[0] == 192 && b[1] == 0 && b[2] == 0) // IETF protocol assignments
            || (b[0] == 192 && b[1] == 0 && b[2] == 2) // documentation
            || (b[0] == 192 && b[1] == 168) // private
            || (b[0] == 198 && b[1] is 18 or 19) // benchmarking
            || (b[0] == 198 && b[1] == 51 && b[2] == 100) // documentation
            || (b[0] == 203 && b[1] == 0 && b[2] == 113) // documentation
            || b[0] >= 224); // multicast, reserved, broadcast
    }

    private static bool IsPublicV6(IPAddress address)
    {
        // Forms that carry an IPv4 address must pass the IPv4 rules, or "::ffff:127.0.0.1" would slip through.
        if (address.IsIPv4MappedToIPv6)
        {
            return IsPublicV4(address.MapToIPv4().GetAddressBytes());
        }

        Span<byte> b = stackalloc byte[16];
        address.TryWriteBytes(b, out _);

        if (b[..15].IndexOfAnyExcept((byte)0) < 0 && b[15] is 0 or 1)
        {
            return false; // :: and ::1
        }

        if (b[0] == 0x00 && b[1] == 0x64 && b[2] == 0xff && b[3] == 0x9b && b[4..12].IndexOfAnyExcept((byte)0) < 0)
        {
            return IsPublicV4(b[12..]); // NAT64 64:ff9b::/96
        }

        if (b[0] == 0x20 && b[1] == 0x02)
        {
            return IsPublicV4(b[2..6]); // 6to4 2002::/16
        }

        return !(
            (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x00 && b[3] == 0x00) // Teredo 2001::/32
            || (b[0] == 0x20 && b[1] == 0x01 && b[2] == 0x0d && b[3] == 0xb8) // documentation 2001:db8::/32
            || (b[0] & 0xfe) == 0xfc // unique local fc00::/7
            || (b[0] == 0xfe && (b[1] & 0xc0) == 0x80) // link-local fe80::/10
            || (b[0] == 0xfe && (b[1] & 0xc0) == 0xc0) // site-local fec0::/10
            || b[0] == 0xff); // multicast
    }
}
