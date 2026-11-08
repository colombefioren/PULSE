# 18. Charts

Status: Accepted

Milestone: Design refresh (before M1)

## Decision and rationale

Charts are hand-written SVG with the geometry in `lib/chart.ts` so it can be unit-tested: ring gauge,
sparkline, response-time chart (hover crosshair, p95 line, downtime bands) and 90-day uptime bars. Failed
checks break the line instead of drawing a zero. Uptime shows two decimals once it is in the nines.
