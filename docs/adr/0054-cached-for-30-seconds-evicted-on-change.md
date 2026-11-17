# 54. Cached for 30 seconds, evicted on change

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

Public traffic can be a lot larger than the team's own, so the page is cached in memory per address
(`StatusPage:CacheSeconds`, 0 turns it off) with a per-address lock so a burst builds it once. A miss is
remembered for 10 seconds so a bad address cannot be used to hit the database. Every API instance listens to
`pulse_events` anyway, so a status or incident event drops that team's cached pages at once. The browser is told
`Cache-Control: public, max-age=30`.
