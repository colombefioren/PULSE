using System.Net;

namespace Pulse.Domain.Monitors;

/// <summary>
/// The checks on a monitor address that need no network. The resolved addresses are checked again, by
/// <see cref="NetworkPolicy"/>, when a name is looked up and every time a check connects.
/// </summary>
public static class MonitorUrl
{
    public const int MaxLength = 2000;

    private static readonly string[] BlockedSuffixes = [".localhost", ".local", ".internal", ".localdomain", ".lan", ".home.arpa"];

    /// <summary>Returns the parsed address, or the reason it is not allowed.</summary>
    public static (Uri? Uri, string? Error) Parse(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return (null, "Enter the address to check.");
        }

        if (text.Length > MaxLength)
        {
            return (null, $"Use an address shorter than {MaxLength} characters.");
        }

        if (!Uri.TryCreate(text.Trim(), UriKind.Absolute, out var uri))
        {
            return (null, "Enter a full address like https://example.com/health.");
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return (null, "Only http:// and https:// addresses can be checked.");
        }

        if (!string.IsNullOrEmpty(uri.UserInfo))
        {
            return (null, "Remove the username and password from the address and use a header instead.");
        }

        var host = uri.IdnHost.TrimEnd('.');
        if (host.Length == 0)
        {
            return (null, "That address has no host name.");
        }

        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || BlockedSuffixes.Any(suffix => host.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)))
        {
            return (null, "That host name points at a private network, which Pulse cannot reach.");
        }

        if (IPAddress.TryParse(uri.Host.Trim('[', ']'), out var literal) && !NetworkPolicy.IsPublic(literal))
        {
            return (null, "That is a private or internal address, which Pulse cannot reach.");
        }

        return (uri, null);
    }
}
