# 11. Logging bootstrap

Status: Accepted

Milestone: M0 - Foundations

## Decision and rationale

The API uses a plain Serilog bootstrap logger rather than a reloadable one. A reloadable logger can only
be frozen once, which breaks tests that start several hosts in one process.
