# 37. Scheduling without a queue

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

The worker needs no broker. Each second the scheduler claims due monitors with
`SELECT ... FOR UPDATE SKIP LOCKED` and moves `next_check_at` forward (with 10% jitter either way so monitors do
not all fire on the same second) in the same transaction. That is an at-most-once lease: if a worker dies after
claiming, that one check is skipped and the next runs on schedule, which is better for monitoring than running
the same check twice. Claimed monitors go into a bounded channel (1000) drained by a fixed number of runners
(32), so a slow target cannot starve the others. A second worker is safe to run.
