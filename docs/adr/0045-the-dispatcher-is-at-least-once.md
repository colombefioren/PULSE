# 45. The dispatcher is at-least-once

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

Workers claim due outbox rows with `FOR UPDATE SKIP LOCKED` and take a 5 minute lease, then send outside the
transaction (up to 8 at a time). A crash after sending but before recording leaves the lease to expire and the
message is sent again. For an alert, a duplicate is far better than a miss, and every delivery has a stable id
(`X-Pulse-Delivery`) so receivers can deduplicate. Failures retry after 1, 5, 15 and 60 minutes, five attempts in
all, then the delivery is marked failed and stays visible in the channel's history.
