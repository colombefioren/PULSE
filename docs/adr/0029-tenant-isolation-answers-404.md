# 29. Tenant isolation answers 404

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

`OrgAccess.RequireAsync` is the single door into a team. A person who is not a member gets 404
`organization_not_found`, the same as for an id that does not exist, so ids cannot be probed. A member whose role
is too low gets 403 `insufficient_role`. The integration suite runs every team endpoint as an outsider.
