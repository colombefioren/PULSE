# 51. The public page is built, not stored

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

`PublicStatusService.GetAsync` assembles the page on request: components and their monitors, the last 90 days
of uptime, and the relevant incidents. Days before yesterday come from `monitor_daily_stats`; yesterday and today
come from the raw `check_results`, so the newest bars are right whether or not the rollup job has run. A
day's uptime is the lower of the share of passing checks and 100 minus the share of the day an incident was open,
so a manual outage the checks never saw still marks the bar. The 90 day figure is the mean of the days that have
data, and a day without checks shows grey rather than a false 100 percent.
