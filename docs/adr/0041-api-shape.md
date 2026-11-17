# 41. API shape

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

- Routes stay under `/api`, not `/api/v1`: there are no outside consumers yet, and versioning an API nobody else
  uses only doubles the surface.
- `PATCH /api/monitors/{id}` replaces all editable fields. It never touches state; partial updates would force
  every client to track what it did not send.
- Request headers (for example an `Authorization` token) are stored as plain jsonb. Viewers receive the header
  names with masked values. Encrypting them at rest is on the list for M5, alongside secret rotation.
- Limits: 50 monitors per team (409 `monitor_limit_reached`), 10 headers, intervals 30 s / 1 / 5 / 10 / 30 min,
  timeout 1-30 s and never longer than the interval.
