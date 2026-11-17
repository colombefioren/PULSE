# 50. The dashboard is the summary endpoint

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

`GET /api/orgs/{id}/summary` returns the counts, open incidents and uptime for 24 hours, 7 and 30 days, all
aggregated in PostgreSQL, and `/series` returns the response-time series. Incident and status events from the
socket invalidate them, with a one minute poll as a safety net.
