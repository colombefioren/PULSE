# 47. Outgoing requests get the same SSRF treatment as checks

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

Webhook sends use a dedicated client with no proxy and no redirects, a 10 second timeout, and the same
connect-time address guard as the check engine (`GuardedConnect`). A webhook that answers with a redirect is
treated as a failure rather than followed to somewhere the user never approved.
