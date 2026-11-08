using Pulse.Domain.Organizations;

namespace Pulse.Application.Organizations;

public sealed record OrganizationSummary(Guid Id, string Name, string Slug, Role Role);

public sealed record OrganizationDetail(Guid Id, string Name, string Slug, Role Role, DateTimeOffset CreatedAt, int MemberCount);

public sealed record MemberDto(Guid UserId, string Email, string Name, Role Role, DateTimeOffset JoinedAt);

public sealed record InviteDto(Guid Id, string Email, Role Role, DateTimeOffset ExpiresAt, DateTimeOffset CreatedAt);

public sealed record InvitePreview(string OrganizationName, string Email, Role Role);

public sealed record CreateOrganizationRequest(string Name);

public sealed record UpdateOrganizationRequest(string Name);

public sealed record ChangeRoleRequest(Role Role);

public sealed record CreateInviteRequest(string Email, Role Role);
