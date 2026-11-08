using Microsoft.AspNetCore.SignalR;
using Pulse.Application.StatusPages;

namespace Pulse.Api.Live;

/// <summary>
/// Lets anyone watching a public status page hear when it changes. There is no sign-in: a connection names the
/// page it is reading and is told only that something changed, then fetches the (cached) public page itself, so
/// nothing private can travel over this hub.
/// </summary>
public sealed class StatusHub(PublicStatusService pages) : Hub
{
    public const string Path = "/hubs/status";

    public static string GroupFor(Guid statusPageId) => $"status:{statusPageId}";

    public async Task Subscribe(string slug)
    {
        var pageId = await pages.PageIdAsync(slug ?? string.Empty, Context.ConnectionAborted)
            ?? throw new HubException("status_page_not_found");

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(pageId), Context.ConnectionAborted);
    }
}
