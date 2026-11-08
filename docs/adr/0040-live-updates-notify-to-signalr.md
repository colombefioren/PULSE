# 40. Live updates: NOTIFY to SignalR

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

The worker writes a `pg_notify('pulse_events', ...)` after each result; every API instance holds one dedicated
`LISTEN` connection (reconnecting with back-off) and forwards events to a SignalR group per organization. There
is no shared message bus and no sticky sessions. Notes:
- `Subscribe(orgId)` checks membership on the server, so a connection only ever hears its own team.
- Group membership is not revoked when someone is removed from a team mid-session. Their connection ends with
  their cookie and the window is short; revisit if sessions become long-lived.
- Browsers connect with WebSockets only and `skipNegotiation`, because the negotiate request is a POST that
  would need an antiforgery token. The cookie authenticates the socket, and the allowed origin is the origin of
  `App:PublicUrl`.
- The SignalR client is a lazy chunk (about 14 KB gzipped) loaded after sign-in, outside the first page load.
- If the socket cannot open (blocked network, or a browser that does not treat `'self'` as covering `wss:` in
  `connect-src`), screens fall back to a refetch every minute and keep working.
