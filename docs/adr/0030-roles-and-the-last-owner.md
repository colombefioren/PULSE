# 30. Roles and the last Owner

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

Three roles: Owner, Admin, Viewer. Only an Owner can grant, change or revoke Owner, and a team always keeps at
least one (409 `last_owner`), including when the last Owner tries to leave. Deleting a team cascades through the
database foreign keys.
