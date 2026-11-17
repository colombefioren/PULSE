# 55. A hub that only says "changed"

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

`/hubs/status` needs no sign-in, so it must never carry data. A client names a page, joins its group and receives
`StatusChanged`; it then fetches the (cached) public page like any visitor. A socket that drops falls back to a
one minute poll.
