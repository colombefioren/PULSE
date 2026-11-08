# 43. An incident is the monitor's state change, nothing more

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

A monitor going Down opens one incident and coming back Up resolves it. The thresholds on the monitor (down after
N failures, up after M passes) already decide when that happens, so there is no second evaluator to disagree with
the first. A partial unique index (`ux_incidents_open_per_monitor`) lets the database refuse a second open
incident for a monitor, whatever a bug or a race tries. Pausing, deleting or changing what a monitor checks
closes its open incident quietly, with no alert, because nothing recovered.
