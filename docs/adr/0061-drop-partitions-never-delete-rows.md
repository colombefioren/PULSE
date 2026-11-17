# 61. Drop partitions, never delete rows

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

`check_results` is the one table that grows without bound, so it is partitioned by month and retention is
`DROP TABLE` on a whole partition once its last day is older than 30 days. A `DELETE` of that many rows would
bloat the table, take locks and need vacuuming; dropping a partition is instant and returns the space. Before a
month is dropped the worker runs the daily and hourly rollups over it, so nothing the product shows depends on the
raw rows afterwards. Rollups are idempotent upserts, so running them twice, or after a restart, is harmless. The
partition names come from `pg_inherits` and must match `check_results_YYYY_MM`, so nothing a user types reaches
the statement. Hourly rollups (a small table) are kept 365 days with a plain delete; daily rollups are kept forever.
