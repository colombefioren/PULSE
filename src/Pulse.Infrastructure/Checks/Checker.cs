using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Security.Authentication;
using System.Text;
using Pulse.Domain.Monitors;

namespace Pulse.Infrastructure.Checks;

/// <summary>Everything a runner needs to perform one check, copied out of the monitor so no database is touched while it runs.</summary>
public sealed record CheckTarget(
    Guid MonitorId,
    Guid OrganizationId,
    string Name,
    string Url,
    CheckMethod Method,
    int TimeoutMs,
    string ExpectedCodes,
    string? Keyword,
    IReadOnlyDictionary<string, string> Headers)
{
    public static CheckTarget From(HttpMonitor monitor) => new(
        monitor.Id,
        monitor.OrganizationId,
        monitor.Name,
        monitor.Url,
        monitor.Method,
        monitor.TimeoutMs,
        monitor.ExpectedCodes,
        monitor.Keyword,
        new Dictionary<string, string>(monitor.Headers));
}

public interface IHttpChecker
{
    /// <summary>Performs one check. Ordinary failures (timeouts, refused connections, wrong status) are results, not exceptions.</summary>
    Task<CheckOutcome> CheckAsync(CheckTarget target, CancellationToken cancellationToken);
}

/// <summary>Which addresses a check may connect to. The default allows public addresses only.</summary>
public interface IAddressPolicy
{
    bool IsAllowed(IPAddress address);
}

public sealed class PublicAddressPolicy : IAddressPolicy
{
    public bool IsAllowed(IPAddress address) => NetworkPolicy.IsPublic(address);
}

/// <summary>Thrown from the connect step when a host resolves to an address the policy refuses.</summary>
public sealed class BlockedAddressException(string host) : Exception($"{host} resolves to a private or internal address.");

/// <summary>
/// Makes the HTTP request for a check. The SSRF guard lives in the connect step, so it applies to every
/// connection the request makes, including each redirect, and to the address actually used rather than the
/// one a name resolved to earlier. Proxies are off, because a proxy would make that connection for us.
/// </summary>
public sealed class HttpChecker : IHttpChecker, IDisposable
{
    public const int MaxBodyBytes = 1_048_576;
    public const string UserAgent = "Pulse/1.0 (+https://github.com/colombefioren/PULSE)";

    private readonly HttpClient _client;
    private readonly IAddressPolicy _policy;

    public HttpChecker(IAddressPolicy policy)
    {
        _policy = policy;
        _client = new HttpClient(
            new SocketsHttpHandler
            {
                UseProxy = false,
                UseCookies = false,
                AllowAutoRedirect = true,
                MaxAutomaticRedirections = 5,
                AutomaticDecompression = DecompressionMethods.All,
                PooledConnectionLifetime = TimeSpan.FromMinutes(2),
                ConnectCallback = ConnectAsync,
            },
            disposeHandler: true)
        {
            // Each check has its own timeout, set per request.
            Timeout = Timeout.InfiniteTimeSpan,
        };
    }

    public async Task<CheckOutcome> CheckAsync(CheckTarget target, CancellationToken cancellationToken)
    {
        var uri = new Uri(target.Url);
        var codes = ExpectedCodes.Parse(target.ExpectedCodes) ?? ExpectedCodes.DefaultCodes;
        var clock = System.Diagnostics.Stopwatch.StartNew();

        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(target.TimeoutMs);

        try
        {
            using var request = BuildRequest(target, uri);
            using var response = await _client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
            var status = (int)response.StatusCode;

            if (!codes.Matches(status))
            {
                return CheckOutcome.Failed(
                    CheckErrorType.UnexpectedStatus,
                    $"Expected status {codes.Describe()} but received {status} {response.ReasonPhrase}".TrimEnd(),
                    Elapsed(clock),
                    status);
            }

            if (!string.IsNullOrEmpty(target.Keyword))
            {
                var found = await BodyContainsAsync(response, target.Keyword, timeout.Token);
                if (!found)
                {
                    return CheckOutcome.Failed(CheckErrorType.KeywordMissing, $"The response did not contain \"{target.Keyword}\"", Elapsed(clock), status);
                }
            }

            return CheckOutcome.Passed(status, Elapsed(clock));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return CheckOutcome.Failed(CheckErrorType.Timeout, $"Timeout after {FormatSeconds(target.TimeoutMs)} — the server didn't respond", Elapsed(clock));
        }
        catch (HttpRequestException ex)
        {
            return Classify(ex, uri, Elapsed(clock));
        }
        catch (Exception ex) when (ex is InvalidOperationException or IOException or UriFormatException or NotSupportedException)
        {
            return CheckOutcome.Failed(CheckErrorType.Other, ex.Message, Elapsed(clock));
        }
    }

    public void Dispose() => _client.Dispose();

    private static HttpRequestMessage BuildRequest(CheckTarget target, Uri uri)
    {
        var method = target.Method switch
        {
            CheckMethod.Head => HttpMethod.Head,
            CheckMethod.Post => HttpMethod.Post,
            _ => HttpMethod.Get,
        };

        var request = new HttpRequestMessage(method, uri);
        if (method == HttpMethod.Post)
        {
            request.Content = new StringContent(string.Empty);
            request.Content.Headers.ContentType = null;
        }

        request.Headers.UserAgent.ParseAdd(UserAgent);
        foreach (var (name, value) in target.Headers)
        {
            if (!request.Headers.TryAddWithoutValidation(name, value))
            {
                request.Content?.Headers.TryAddWithoutValidation(name, value);
            }
        }

        return request;
    }

    private static async Task<bool> BodyContainsAsync(HttpResponseMessage response, string keyword, CancellationToken cancellationToken)
    {
        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var limited = new LimitedStream(stream, MaxBodyBytes);
        var encoding = ResponseEncoding(response.Content.Headers.ContentType);
        using var reader = new StreamReader(limited, encoding, detectEncodingFromByteOrderMarks: true);
        var text = await reader.ReadToEndAsync(cancellationToken);

        return text.Contains(keyword, StringComparison.Ordinal);
    }

    private static Encoding ResponseEncoding(MediaTypeHeaderValue? contentType)
    {
        try
        {
            return contentType?.CharSet is { Length: > 0 } charset ? Encoding.GetEncoding(charset) : Encoding.UTF8;
        }
        catch (ArgumentException)
        {
            return Encoding.UTF8;
        }
    }

    private async ValueTask<Stream> ConnectAsync(SocketsHttpConnectionContext context, CancellationToken cancellationToken)
    {
        var host = context.DnsEndPoint.Host;
        var addresses = IPAddress.TryParse(host.Trim('[', ']'), out var literal)
            ? [literal]
            : await Dns.GetHostAddressesAsync(host, cancellationToken);

        if (addresses.Length == 0)
        {
            throw new SocketException((int)SocketError.HostNotFound);
        }

        // One private record among public ones is enough to refuse: a name must not be able to steer the check inward.
        if (addresses.Any(address => !_policy.IsAllowed(address)))
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

    private static CheckOutcome Classify(HttpRequestException error, Uri uri, int elapsedMs)
    {
        var port = uri.Port;
        for (Exception? current = error; current is not null; current = current.InnerException)
        {
            switch (current)
            {
                case BlockedAddressException:
                    return CheckOutcome.Failed(CheckErrorType.Blocked, $"Pulse will not connect to {uri.IdnHost}: it points at a private or internal address", elapsedMs);
                case SocketException { SocketErrorCode: SocketError.HostNotFound or SocketError.TryAgain or SocketError.NoData }:
                    return CheckOutcome.Failed(CheckErrorType.Dns, $"DNS lookup failed — {uri.IdnHost} could not be resolved", elapsedMs);
                case SocketException { SocketErrorCode: SocketError.ConnectionRefused }:
                    return CheckOutcome.Failed(CheckErrorType.ConnectionRefused, $"Connection refused by {uri.IdnHost}:{port}", elapsedMs);
                case SocketException { SocketErrorCode: SocketError.TimedOut }:
                    return CheckOutcome.Failed(CheckErrorType.Timeout, $"Timed out connecting to {uri.IdnHost}:{port}", elapsedMs);
                case AuthenticationException auth:
                    return CheckOutcome.Failed(CheckErrorType.Tls, $"TLS handshake failed — {auth.Message}", elapsedMs);
            }
        }

        if (error.HttpRequestError == HttpRequestError.SecureConnectionError)
        {
            return CheckOutcome.Failed(CheckErrorType.Tls, $"TLS handshake failed — {error.Message}", elapsedMs);
        }

        var inner = error.InnerException?.Message;
        return CheckOutcome.Failed(CheckErrorType.Other, inner is null ? error.Message : $"{error.Message} ({inner})", elapsedMs);
    }

    private static int Elapsed(System.Diagnostics.Stopwatch clock) => (int)Math.Min(clock.ElapsedMilliseconds, int.MaxValue);

    private static string FormatSeconds(int milliseconds) =>
        milliseconds % 1000 == 0 ? $"{milliseconds / 1000}s" : $"{milliseconds / 1000d:0.#}s";

    /// <summary>Stops reading after a fixed number of bytes, so a huge or endless body cannot tie up a runner.</summary>
    private sealed class LimitedStream(Stream inner, int limit) : Stream
    {
        private int _remaining = limit;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => Read(buffer.AsSpan(offset, count));

        public override int Read(Span<byte> buffer)
        {
            if (_remaining <= 0)
            {
                return 0;
            }

            var read = inner.Read(buffer[..Math.Min(buffer.Length, _remaining)]);
            _remaining -= read;
            return read;
        }

        public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
        {
            if (_remaining <= 0)
            {
                return 0;
            }

            var read = await inner.ReadAsync(buffer[..Math.Min(buffer.Length, _remaining)], cancellationToken);
            _remaining -= read;
            return read;
        }

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    }
}
