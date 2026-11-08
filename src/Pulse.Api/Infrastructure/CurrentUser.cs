using System.Security.Claims;
using Pulse.Application.Common;

namespace Pulse.Api.Infrastructure;

public static class CurrentUser
{
    /// <summary>The signed-in user's id. Endpoints that call this already sit behind RequireAuthorization.</summary>
    public static Guid Id(this ClaimsPrincipal principal) =>
        Guid.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), out var id)
            ? id
            : throw AppException.Unauthorized("not_signed_in", "Sign in to continue.");
}
