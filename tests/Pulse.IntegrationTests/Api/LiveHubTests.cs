using System.Text.Json;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.SignalR.Client;
using Pulse.IntegrationTests.Support;

namespace Pulse.IntegrationTests.Api;

/// <summary>Events written to PostgreSQL reach exactly the browsers of the organization they belong to.</summary>
[Collection(ApiCollection.Name)]
public class LiveHubTests(PulseApi api)
{
    private HubConnection Connect(TestUser user)
    {
        var socketClient = api.Server.CreateWebSocketClient();
        socketClient.ConfigureRequest = request => request.Headers["Cookie"] = user.Client.AuthCookie;

        return new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/live"), options =>
            {
                options.SkipNegotiation = true;
                options.Transports = HttpTransportType.WebSockets;
                options.WebSocketFactory = (context, token) => new ValueTask<System.Net.WebSockets.WebSocket>(socketClient.ConnectAsync(context.Uri, token));
            })
            .Build();
    }

    private Task Notify(Guid organizationId, string type, object data) =>
        api.Execute(
            "select pg_notify('pulse_events', $1)",
            JsonSerializer.Serialize(new { type, org = organizationId, data }));

    [Fact]
    public async Task A_member_hears_their_organizations_events()
    {
        var owner = await api.SignUp("live-owner");
        await using var connection = Connect(owner);
        var heard = new TaskCompletionSource<JsonElement>();
        connection.On<JsonElement>("CheckRecorded", data => heard.TrySetResult(data));
        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe", owner.OrgId);

        await Notify(owner.OrgId, "CheckRecorded", new { monitorId = Guid.NewGuid(), responseMs = 123 });

        var data = await heard.Task.WaitAsync(TimeSpan.FromSeconds(10));
        data.GetProperty("responseMs").GetInt32().Should().Be(123);
    }

    [Fact]
    public async Task Another_organizations_events_are_not_delivered()
    {
        var owner = await api.SignUp("live-a");
        var other = await api.SignUp("live-b");
        await using var connection = Connect(owner);
        var heard = new List<string>();
        connection.On<JsonElement>("CheckRecorded", data => heard.Add(data.GetRawText()));
        var own = new TaskCompletionSource();
        connection.On<JsonElement>("Marker", _ => own.TrySetResult());
        await connection.StartAsync();
        await connection.InvokeAsync("Subscribe", owner.OrgId);

        await Notify(other.OrgId, "CheckRecorded", new { secret = "not for you" });
        await Notify(owner.OrgId, "Marker", new { });
        await own.Task.WaitAsync(TimeSpan.FromSeconds(10));

        heard.Should().BeEmpty();
    }

    [Fact]
    public async Task A_non_member_cannot_subscribe_to_an_organization()
    {
        var victim = await api.SignUp("live-victim");
        var intruder = await api.SignUp("live-intruder");
        await using var connection = Connect(intruder);
        await connection.StartAsync();

        var act = () => connection.InvokeAsync("Subscribe", victim.OrgId);

        (await act.Should().ThrowAsync<HubException>()).Which.Message.Should().Contain("organization_not_found");
    }

    [Fact]
    public async Task Signed_out_visitors_cannot_connect()
    {

        var socketClient = api.Server.CreateWebSocketClient();
        await using var connection = new HubConnectionBuilder()
            .WithUrl(new Uri(api.Server.BaseAddress, "/hubs/live"), options =>
            {
                options.SkipNegotiation = true;
                options.Transports = HttpTransportType.WebSockets;
                options.WebSocketFactory = (context, token) => new ValueTask<System.Net.WebSockets.WebSocket>(socketClient.ConnectAsync(context.Uri, token));
            })
            .Build();

        var act = () => connection.StartAsync();

        await act.Should().ThrowAsync<Exception>();
    }
}
