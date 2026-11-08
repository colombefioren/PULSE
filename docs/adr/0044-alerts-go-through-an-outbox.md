# 44. Alerts go through an outbox

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

The incident, its timeline entry, one `alert_deliveries` row and one `outbox_messages` row per channel are
written in the same transaction as the check result. If that save fails nothing was announced, and if it succeeds
the alert cannot be lost even if the worker dies a millisecond later. A unique index on (channel, incident, kind)
means a channel is never alerted twice for the same change, however often the recorder retries.
