# 38. Check history is partitioned by month

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

`check_results` is range-partitioned by `checked_at` with primary key `(monitor_id, checked_at)` and cascades
from `monitors`. Dropping an old month is a metadata operation, which is how retention will work later. The
migration creates the current and next two months; the worker tops that up every 6 hours and the recorder
creates a missing partition and retries if an insert hits one. The table has no `organization_id`: ownership is
reached through the monitor, and the API always checks the monitor first.
