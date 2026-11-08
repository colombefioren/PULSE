# 34. The entity is `HttpMonitor`

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

`Monitor` collides with `System.Threading.Monitor` in every file that uses locks or implicit usings, so the
entity is named for what it is. The table is still `monitors`; a TCP or heartbeat monitor later would be another
type with its own checker.
