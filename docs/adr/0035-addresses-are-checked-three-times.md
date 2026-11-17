# 35. Addresses are checked three times

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

Monitoring a URL someone else typed is a server-side request they control, so it is guarded in layers:
1. `MonitorUrl.Parse` accepts only `http` and `https`, no user info, and refuses `localhost`, `.local`,
   `.internal` and literal private addresses.
2. `DnsUrlGuard` resolves the name when a monitor is saved and refuses names that point at private space.
3. `HttpChecker` re-checks at connect time: its `ConnectCallback` resolves the name itself and refuses the
   connection if any returned address is non-public. This is the one that matters, because DNS can change
   between save and check (rebinding), and redirects can lead anywhere.

`NetworkPolicy.IsPublic` rejects loopback, RFC 1918, link-local (including the 169.254.169.254 metadata
address), CGNAT, multicast, reserved ranges, IPv6 unique-local and link-local, and the IPv6 forms that wrap an
IPv4 address (mapped, NAT64, 6to4, Teredo). Proxies are off, redirects are followed up to 5 times and every hop
goes through the same connect check.
