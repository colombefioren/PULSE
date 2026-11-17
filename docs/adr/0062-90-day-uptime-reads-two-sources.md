# 62. 90-day uptime reads two sources

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

Days before yesterday come from the daily totals; yesterday and today come from the raw checks. The rollup job
refreshes yesterday and today every ten minutes, but reading them raw means the figure is correct even if the job
has not run, and a freshly dropped partition can never leave a gap.
