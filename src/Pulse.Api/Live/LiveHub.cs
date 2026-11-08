using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using Pulse.Api.Infrastructure;
using Pulse.Application.Abstractions;

namespace Pulse.Api.Live;

/// <summary>
/// Pushes live updates to browsers. A connection hears nothing until it subscribes to an organization, and a
/// subscription is only granted to members, so one team's checks never reach another team's screen.
/// </summary>
[Authorize]
public sealed class LiveHub(IAppDbContext db) : Hub
{
    public const string Path = "/hubs/live";

    public static string GroupFor(Guid organizationId) => $"org:{organizationId}";

    public async Task Subscribe(Guid organizationId)
    {
        var userId = Context.User!.Id();
        var isMember = await db.Memberships.AnyAsync(m => m.OrganizationId == organizationId && m.UserId == userId, Context.ConnectionAborted);
        if (!isMember)
        {
            throw new HubException("organization_not_found");
        }

        await Groups.AddToGroupAsync(Context.ConnectionId, GroupFor(organizationId), Context.ConnectionAborted);
    }

    public Task Unsubscribe(Guid organizationId) =>
        Groups.RemoveFromGroupAsync(Context.ConnectionId, GroupFor(organizationId), Context.ConnectionAborted);
}
