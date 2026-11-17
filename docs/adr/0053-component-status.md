# 53. Component status

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

Down means every active monitor is down, degraded means some are, maintenance means every monitor behind it is
paused (or an open Maintenance incident), up is the rest. A monitor that has not been checked yet counts as up
rather than alarming a customer. An open manual MajorOutage marks the component down; PartialOutage and Degraded
mark it degraded. Maintenance incidents do not count as downtime.
