using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Pulse.Domain.Common;
using Pulse.Domain.Monitors;
using Pulse.Infrastructure.Checks;

namespace Pulse.IntegrationTests.Engine;

/// <summary>Lets checks reach 127.0.0.1 so a real local server can stand in for "the internet".</summary>
public sealed class LoopbackPolicy : IAddressPolicy
{
    public bool IsAllowed(IPAddress address) => IPAddress.IsLoopback(address) || NetworkPolicy.IsPublic(address);
}

/// <summary>A small web server on a random local port that behaves however a test needs.</summary>
public sealed class TargetServer : IAsyncLifetime
{
    private WebApplication _app = null!;

    public string BaseUrl { get; private set; } = string.Empty;

    public string? LastUserAgent { get; private set; }

    public string? LastCustomHeader { get; private set; }

    public string? LastMethod { get; private set; }

    public async Task InitializeAsync()
    {
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0");
        builder.Logging.ClearProviders();
        _app = builder.Build();

        _app.MapMethods("/ok", ["GET", "HEAD", "POST"], (HttpContext context) =>
        {
            LastUserAgent = context.Request.Headers.UserAgent;
            LastCustomHeader = context.Request.Headers["X-Check"];
            LastMethod = context.Request.Method;
            return Results.Text("<html>all systems go</html>");
        });
        _app.MapGet("/fail", () => Results.StatusCode(503));
        _app.MapGet("/redirect", () => Results.Redirect("/ok"));
        _app.MapGet("/slow", async () =>
        {
            await Task.Delay(TimeSpan.FromSeconds(10));
            return Results.Ok();
        });
        _app.MapGet("/huge", async (HttpContext context) =>
        {
            context.Response.ContentType = "text/plain";
            var chunk = new string('x', 64 * 1024);
            for (var i = 0; i < 40; i++)
            {
                await context.Response.WriteAsync(chunk);
            }

            await context.Response.WriteAsync("needle");
        });

        await _app.StartAsync();
        BaseUrl = _app.Services.GetRequiredService<IServer>().Features.Get<IServerAddressesFeature>()!.Addresses.First();
    }

    public async Task DisposeAsync() => await _app.DisposeAsync();
}

public class HttpCheckerTests : IClassFixture<TargetServer>, IDisposable
{
    private readonly TargetServer _server;
    private readonly HttpChecker _checker = new(new LoopbackPolicy());

    public HttpCheckerTests(TargetServer server) => _server = server;

    public void Dispose() => _checker.Dispose();

    private CheckTarget Target(string path, CheckMethod method = CheckMethod.Get, string codes = "200-299", string? keyword = null, int timeoutMs = 5000, Dictionary<string, string>? headers = null) =>
        new(Ids.New(), Ids.New(), "target", _server.BaseUrl.TrimEnd('/') + path, method, timeoutMs, codes, keyword, headers ?? []);

    [Fact]
    public async Task A_healthy_response_passes_with_its_status_and_timing()
    {
        var outcome = await _checker.CheckAsync(Target("/ok"), default);

        outcome.Success.Should().BeTrue();
        outcome.StatusCode.Should().Be(200);
        outcome.ResponseMs.Should().BeInRange(0, 5000);
        outcome.ErrorType.Should().BeNull();
    }

    [Fact]
    public async Task An_unexpected_status_fails_and_says_what_was_expected_and_received()
    {
        var outcome = await _checker.CheckAsync(Target("/fail"), default);

        outcome.Success.Should().BeFalse();
        outcome.ErrorType.Should().Be(CheckErrorType.UnexpectedStatus);
        outcome.StatusCode.Should().Be(503);
        outcome.ErrorMessage.Should().Contain("200–299").And.Contain("503");
    }

    [Fact]
    public async Task A_status_listed_as_expected_passes()
    {
        var outcome = await _checker.CheckAsync(Target("/fail", codes: "200-299, 503"), default);

        outcome.Success.Should().BeTrue();
    }

    [Fact]
    public async Task A_keyword_must_be_in_the_body()
    {
        (await _checker.CheckAsync(Target("/ok", keyword: "all systems go"), default)).Success.Should().BeTrue();

        var missing = await _checker.CheckAsync(Target("/ok", keyword: "maintenance"), default);
        missing.Success.Should().BeFalse();
        missing.ErrorType.Should().Be(CheckErrorType.KeywordMissing);
        missing.ErrorMessage.Should().Contain("maintenance");
    }

    [Fact]
    public async Task Only_the_first_megabyte_of_a_body_is_searched()
    {
        // The needle sits after 2.5 MB of filler, past the limit.
        var outcome = await _checker.CheckAsync(Target("/huge", keyword: "needle"), default);

        outcome.ErrorType.Should().Be(CheckErrorType.KeywordMissing);
    }

    [Fact]
    public async Task A_slow_server_times_out_with_a_plain_message()
    {
        var outcome = await _checker.CheckAsync(Target("/slow", timeoutMs: 1000), default);

        outcome.Success.Should().BeFalse();
        outcome.ErrorType.Should().Be(CheckErrorType.Timeout);
        outcome.ErrorMessage.Should().Be("Timeout after 1s — the server didn't respond");
        outcome.ResponseMs.Should().BeInRange(900, 4000);
    }

    [Fact]
    public async Task A_closed_port_is_reported_as_a_refused_connection()
    {
        var target = Target("/ok") with { Url = "http://127.0.0.1:1/" };

        var outcome = await _checker.CheckAsync(target, default);

        outcome.ErrorType.Should().Be(CheckErrorType.ConnectionRefused);
        outcome.ErrorMessage.Should().Contain("127.0.0.1:1");
    }

    [Fact]
    public async Task A_name_that_does_not_exist_is_a_dns_failure()
    {
        var target = Target("/ok") with { Url = "http://pulse-no-such-host.invalid/" };

        var outcome = await _checker.CheckAsync(target, default);

        outcome.ErrorType.Should().Be(CheckErrorType.Dns);
        outcome.ErrorMessage.Should().Contain("pulse-no-such-host.invalid");
    }

    [Fact]
    public async Task Redirects_are_followed_to_the_final_status()
    {
        var outcome = await _checker.CheckAsync(Target("/redirect"), default);

        outcome.Success.Should().BeTrue();
        outcome.StatusCode.Should().Be(200);
    }

    [Fact]
    public async Task Sends_the_configured_headers_method_and_a_pulse_user_agent()
    {
        var outcome = await _checker.CheckAsync(Target("/ok", CheckMethod.Post, headers: new() { ["X-Check"] = "abc" }), default);

        outcome.Success.Should().BeTrue();
        _server.LastMethod.Should().Be("POST");
        _server.LastCustomHeader.Should().Be("abc");
        _server.LastUserAgent.Should().StartWith("Pulse/");
    }

    [Fact]
    public async Task A_head_request_works()
    {
        var outcome = await _checker.CheckAsync(Target("/ok", CheckMethod.Head), default);

        outcome.Success.Should().BeTrue();
        _server.LastMethod.Should().Be("HEAD");
    }

    [Fact]
    public async Task The_default_policy_refuses_loopback_at_connect_time()
    {
        using var strict = new HttpChecker(new PublicAddressPolicy());

        var outcome = await strict.CheckAsync(Target("/ok"), default);

        outcome.Success.Should().BeFalse();
        outcome.ErrorType.Should().Be(CheckErrorType.Blocked);
        outcome.ErrorMessage.Should().Contain("private or internal");
    }

    [Fact]
    public async Task A_name_that_resolves_to_loopback_is_refused_even_though_it_looks_public()
    {
        using var strict = new HttpChecker(new PublicAddressPolicy());
        var port = new Uri(_server.BaseUrl).Port;

        // "localhost" is only text until it is resolved; the check is on the address it turns into.
        var outcome = await strict.CheckAsync(Target("/ok") with { Url = $"http://localhost:{port}/" }, default);

        outcome.ErrorType.Should().Be(CheckErrorType.Blocked);
    }

    [Fact]
    public async Task Shutting_down_cancels_instead_of_reporting_a_failure()
    {
        using var cancel = new CancellationTokenSource(TimeSpan.FromMilliseconds(200));

        var act = () => _checker.CheckAsync(Target("/slow", timeoutMs: 20_000), cancel.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }
}
