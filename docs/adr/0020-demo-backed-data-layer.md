# 20. Demo-backed data layer

Status: Accepted

Milestone: Design refresh (before M1)

## Decision and rationale

Until the API exists (M1 to M5) the UI reads from a seeded in-browser workspace, `data/demo.ts`, behind
TanStack Query hooks in `data/queries.ts`. The hooks are the seam: replacing the adapter with real API calls
should not touch any page. Pause, resume and acknowledge are optimistic, as they will be against the API.
