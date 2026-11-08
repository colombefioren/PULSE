# 39. Concurrency

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

Monitors carry PostgreSQL's `xmin` as a concurrency token. A stale edit answers 409 `conflict` instead of
overwriting someone else's change. The recorder retries on a clash because the scheduler and an editor can touch
the same row.
