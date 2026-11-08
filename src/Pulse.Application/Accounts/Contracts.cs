using Pulse.Application.Organizations;

namespace Pulse.Application.Accounts;

public sealed record RegisterRequest(string Name, string TeamName, string Email, string Password);

public sealed record LoginRequest(string Email, string Password, bool Remember = false);

public sealed record VerifyEmailRequest(Guid UserId, string Token);

public sealed record EmailRequest(string Email);

public sealed record ResetPasswordRequest(Guid UserId, string Token, string Password);

public sealed record MeResponse(Guid Id, string Email, string Name, bool EmailConfirmed, IReadOnlyList<OrganizationSummary> Organizations);
