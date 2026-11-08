# Decisions

Choices made where the brief was ambiguous or where reality differed from it. Newest last.

## M0 - Foundations

### .NET 10 and the `.slnx` solution format
The brief asks for the latest .NET LTS. That is .NET 10 (SDK 10.0.x), pinned in `global.json` with
`rollForward: latestFeature`. The SDK's default solution format is now `.slnx`, so the solution is
`Pulse.slnx`.

### FluentAssertions stays on 7.x
FluentAssertions 8 and later moved to a commercial license. Version 7.2.0 is Apache-2.0 and is pinned.

### TypeScript 6.0, not 7
Vite scaffolds TypeScript 7 (the native compiler). `typescript-eslint` needs the compiler API and
supports `>=4.8.4 <6.1.0`, and the brief requires ESLint. TypeScript is pinned to `~6.0` until
`typescript-eslint` supports 7.

### ESLint instead of the template's oxlint
The Vite template ships oxlint. The brief specifies ESLint, so the template linter was replaced with
ESLint, `typescript-eslint` and the React hooks plugin.

### Testcontainers 4.16
Testcontainers 4.7 pulled in `SSH.NET` 2024.2.0, which has known high-severity advisories, and the build
treats warnings as errors. 4.16 does not, so it is used instead of suppressing the advisory.

### Brand yellow is sampled, not approximate
The brief says "approximately `#FAC500`". The value sampled from the provided PNG is `#FAC303`
(RGB 250, 195, 3), used as `--brand`.

### The logo is traced, not redrawn
`docs/brand/pulse-logo.png` is the source. The SVGs in `web/src/assets/` were traced from it with potrace
at 4x resolution, so proportions come from the pixels. The heartbeat line is its own path (a small overlap
with the ring hides the seam) so the icon can draw it in without touching the ring. The logo is a logotype,
so the brief's "no yellow text on white" rule is not applied to it; no other yellow text is used.

### Theme is a `data-theme` attribute set before first paint
An inline script in `index.html` reads the stored choice, falls back to the system setting, and sets
`data-theme` before React loads, so there is no flash. The toggle cycles system, light, dark.

### Deferred shell pieces
- The org switcher arrives with organizations in M1.
- The command palette and its top bar trigger arrive in M5. M0 has no dead control for it.
- "Add your first monitor" on the empty monitors page is disabled until the check engine exists (M2).
- The `C` and `/` shortcuts arrive with the screens they act on. `G then M` and `G then I` work now.

### Content aligns left
The brief rules out centered-everything layouts, so page content is left-aligned to the grid with a
maximum width instead of sitting in a centered column.

### Logging bootstrap
The API uses a plain Serilog bootstrap logger rather than a reloadable one. A reloadable logger can only
be frozen once, which breaks tests that start several hosts in one process.

### CI/CD
- `ci.yml` runs on pull requests to `preprod` and `prod`, and is reused by the two environment workflows.
  The environment workflows run it on every push, so a push is not tested twice.
- `deploy-preprod.yml` runs CI on pushes to `preprod`, then publishes images to GHCR tagged `preprod`.
- `release-prod.yml` runs on pushes to `prod`: CI, the Playwright suite once `tests/e2e` exists, image
  publishing, then it tags `vX.Y.Z` and creates a GitHub release from `CHANGELOG.md`.
- Migrations are a separate job, gated by repository variables `PREPROD_DEPLOY_ENABLED` and
  `PROD_DEPLOY_ENABLED`, and never run on application startup. They need a `dotnet-ef` tool manifest, which
  arrives with the first migration in M1.
- The hosting target for the live demo is not chosen yet, so the workflows stop at publishing images. Rolling
  them out to a host is the next step once one is picked.

### Repository settings
The owner chose to leave branch protection off for now and `preprod` was already the default branch.
The brief's protection and merge-commit-only settings are therefore not applied.

### Commit style
Commits follow the owner's COCO--PORTFOLIO history: `type: lowercase description`, no scope, no
`Co-Authored-By` trailer.

### Local environment notes (this machine only)
- `C:\APPS\nodejs` is a symlink to a path under another user's profile, which makes `npm` crash with
  `EPERM`. Running with `NODE_OPTIONS="--preserve-symlinks --preserve-symlinks-main"` avoids it. CI is unaffected.
- Docker Desktop was not running, so the Docker builds were validated with `docker compose config` only. CI
  builds all three images.

## Design refresh (before M1)

### Visual direction
The owner asked for a more striking design after seeing M0 and sent a pastel dashboard as a reference. What is
taken from it is the layout only: a floating sidebar, a large rounded shell, stat tiles, and a right-hand rail of
ring gauges. The colors stay the original Pulse palette (warm off-white, white cards, near-black ink, brand
yellow `#fac303`), at the owner's instruction. A first attempt copied the reference's mauve palette and borrowed
a cinematic scroll tunnel from another site; the owner rejected both, so the palette is back to the original and
the tunnel, scroll ruler and giant uppercase type are gone. What still holds from the brief: status is never shown
by color alone, motion respects `prefers-reduced-motion`, and every chart has a text equivalent.

### Palette and tokens
Light: canvas `#e8e6e0`, shell `#f8f7f5`, cards `#ffffff`, ink `#0b0b0c`. The sidebar and the gauge rail are near-black in
both themes so the yellow logo always has contrast. Everything is a CSS variable with a dark override, so
components never hold a hex.

### Landing page
A calm page in the app's own language: a floating black header, a hero with a live product preview built from the
real chart components, four feature cards, the live public status page embedded, and a closing call to action.

### Charts
Charts are hand-written SVG with the geometry in `lib/chart.ts` so it can be unit-tested: ring gauge,
sparkline, response-time chart (hover crosshair, p95 line, downtime bands) and 90-day uptime bars. Failed
checks break the line instead of drawing a zero. Uptime shows two decimals once it is in the nines.

### The public status page
The page is an Instatus-style layout: overall banner, active incidents with timelines, components grouped
with a 90-day bar strip each, a response-time graph, 30-day history and an email subscribe form. It needs no
account and is embedded live on the landing page.

### Demo-backed data layer
Until the API exists (M1 to M5) the UI reads from a seeded in-browser workspace, `data/demo.ts`, behind
TanStack Query hooks in `data/queries.ts`. The hooks are the seam: replacing the adapter with real API calls
should not touch any page. Pause, resume and acknowledge are optimistic, as they will be against the API.

### Accounts were not open yet (replaced in M1)
Before M1 the sign in and sign up forms validated inline but submitting said so honestly, with a link to the
demo. Faking a successful login would have misled. M1 replaced that with real accounts.

## M1 - Accounts and organizations

### Cookie sign-in, not tokens
The API signs people in with an HTTP-only, `SameSite=Lax` cookie (`pulse.auth`, 14 days, sliding) through ASP.NET
Core Identity. Script on the page can never read it, which removes the usual place a stolen token comes from.
Unauthenticated and forbidden requests answer 401 and 403 instead of redirecting, because the client is an SPA.

### Antiforgery for every write
Because the browser sends the cookie automatically, every non-GET request must also carry `X-XSRF-TOKEN`.
`GET /api/auth/csrf` returns the token; the client keeps it in memory and refetches after sign in or out, since
tokens are bound to the signed-in identity. A stale token gets one transparent retry.

### Sign up never reveals whether an address exists
Register, forgot-password and resend-verification always answer the same way (202). If the address is already
registered, the email says so instead of the response. This costs a little friendliness and closes account
enumeration.

### Email must be verified before sign in
A correct password for an unverified address answers 403 `email_not_verified`, and the sign in page offers to
resend the link. The check runs after the password check so it does not become an enumeration oracle either.

### Passwords
Minimum length 10, no character-class rules (length beats complexity), 5 failed attempts lock the account for 5
minutes. Hashing is Identity's PBKDF2 default.

### Rate limiting on the auth endpoints
A fixed window policy (`RateLimiting:AuthPermitsPerMinute`, default 20 per client address) covers the auth
endpoints and answers 429 with `Retry-After`. Behind nginx the API must trust `X-Forwarded-For`, otherwise every
caller looks like the proxy; compose enables `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. Do that only where the API is
reachable through the proxy alone.

### Invitations replace "add member"
The brief lists `POST /orgs/{id}/members`. Adding a person who has not agreed, by address alone, would let anyone
attach a stranger to a team. Invitations are used instead: a 32-byte random token is emailed, only its SHA-256 is
stored, it expires after 7 days, and a second invitation to the same address replaces the first. Accepting needs
a signed-in account whose email matches the invitation.

### Tenant isolation answers 404
`OrgAccess.RequireAsync` is the single door into a team. A person who is not a member gets 404
`organization_not_found`, the same as for an id that does not exist, so ids cannot be probed. A member whose role
is too low gets 403 `insufficient_role`. The integration suite runs every team endpoint as an outsider.

### Roles and the last Owner
Three roles: Owner, Admin, Viewer. Only an Owner can grant, change or revoke Owner, and a team always keeps at
least one (409 `last_owner`), including when the last Owner tries to leave. Deleting a team cascades through the
database foreign keys.

### Migrations
Development and the integration tests apply migrations on startup (`Database:MigrateOnStartup`). Deployed
environments leave it off and run `dotnet ef database update` from the gated CI job, so a rolling API restart
never races a schema change. Data Protection keys are stored in the database so sign-in cookies and email tokens
survive restarts and work across instances.

### Real accounts see no fake data
Incidents, status pages and the dashboard still read the seeded workspace, which belongs to the demo (monitors became real in M2).
A real account sees a plain "arrives in an upcoming release" panel on those pages instead of someone else's
numbers. "Try the demo" (`/demo`) switches to the seeded workspace for the tab and signing in switches it off.

### Browser security headers
nginx serves the app with a strict content security policy (own origin only, no inline scripts, which is why the
theme bootstrap is `theme-init.js`), `nosniff`, no framing, a restrictive referrer policy and HSTS. The API sends
its own policy that allows nothing, since it only returns JSON.

## M2 - Monitors and the check engine

### The entity is `HttpMonitor`
`Monitor` collides with `System.Threading.Monitor` in every file that uses locks or implicit usings, so the
entity is named for what it is. The table is still `monitors`; a TCP or heartbeat monitor later would be another
type with its own checker.

### Addresses are checked three times
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

### Check results
- A check passes when the status is in the expected list (default `200-299`) and, if set, the body contains the
  keyword. The keyword is case-sensitive and searched in the first 1 MB only; a HEAD monitor cannot have one.
- Failures name their cause in plain words (`Timeout after 10s - the server didn't respond`,
  `Expected 200-299, got 503`), because the message is what people read at 3 a.m.
- Thresholds: a monitor goes Down after `FailureThreshold` failures in a row and back Up after
  `RecoveryThreshold` passes in a row (defaults 3 and 2). The first result moves Pending straight to Up or to
  counting failures. Results that arrive for a paused monitor are ignored.
- Editing the address, method, status codes, keyword or headers starts the history over (state Pending), since
  old results describe a different check. Renaming or changing the interval does not.

### Scheduling without a queue
The worker needs no broker. Each second the scheduler claims due monitors with
`SELECT ... FOR UPDATE SKIP LOCKED` and moves `next_check_at` forward (with 10% jitter either way so monitors do
not all fire on the same second) in the same transaction. That is an at-most-once lease: if a worker dies after
claiming, that one check is skipped and the next runs on schedule, which is better for monitoring than running
the same check twice. Claimed monitors go into a bounded channel (1000) drained by a fixed number of runners
(32), so a slow target cannot starve the others. A second worker is safe to run.

### Check history is partitioned by month
`check_results` is range-partitioned by `checked_at` with primary key `(monitor_id, checked_at)` and cascades
from `monitors`. Dropping an old month is a metadata operation, which is how retention will work later. The
migration creates the current and next two months; the worker tops that up every 6 hours and the recorder
creates a missing partition and retries if an insert hits one. The table has no `organization_id`: ownership is
reached through the monitor, and the API always checks the monitor first.

### Concurrency
Monitors carry PostgreSQL's `xmin` as a concurrency token. A stale edit answers 409 `conflict` instead of
overwriting someone else's change. The recorder retries on a clash because the scheduler and an editor can touch
the same row.

### Live updates: NOTIFY to SignalR
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

### API shape
- Routes stay under `/api`, not `/api/v1`: there are no outside consumers yet, and versioning an API nobody else
  uses only doubles the surface.
- `PATCH /api/monitors/{id}` replaces all editable fields. It never touches state; partial updates would force
  every client to track what it did not send.
- Request headers (for example an `Authorization` token) are stored as plain jsonb. Viewers receive the header
  names with masked values. Encrypting them at rest is on the list for M5, alongside secret rotation.
- Limits: 50 monitors per team (409 `monitor_limit_reached`), 10 headers, intervals 30 s / 1 / 5 / 10 / 30 min,
  timeout 1-30 s and never longer than the interval.

### Demo stays read-only
In the demo workspace the Add, Edit and Delete controls are hidden: they would have nothing to save to. Pausing
still works because the simulation implements it.

## M3: incidents and alerts

### An incident is the monitor's state change, nothing more
A monitor going Down opens one incident and coming back Up resolves it. The thresholds on the monitor (down after
N failures, up after M passes) already decide when that happens, so there is no second evaluator to disagree with
the first. A partial unique index (`ux_incidents_open_per_monitor`) lets the database refuse a second open
incident for a monitor, whatever a bug or a race tries. Pausing, deleting or changing what a monitor checks
closes its open incident quietly, with no alert, because nothing recovered.

### Alerts go through an outbox
The incident, its timeline entry, one `alert_deliveries` row and one `outbox_messages` row per channel are
written in the same transaction as the check result. If that save fails nothing was announced, and if it succeeds
the alert cannot be lost even if the worker dies a millisecond later. A unique index on (channel, incident, kind)
means a channel is never alerted twice for the same change, however often the recorder retries.

### The dispatcher is at-least-once
Workers claim due outbox rows with `FOR UPDATE SKIP LOCKED` and take a 5 minute lease, then send outside the
transaction (up to 8 at a time). A crash after sending but before recording leaves the lease to expire and the
message is sent again. For an alert, a duplicate is far better than a miss, and every delivery has a stable id
(`X-Pulse-Delivery`) so receivers can deduplicate. Failures retry after 1, 5, 15 and 60 minutes, five attempts in
all, then the delivery is marked failed and stays visible in the channel's history.

### Secrets are encrypted and shown once
A channel's settings (recipients, webhook address, signing secret) are JSON encrypted with ASP.NET Data
Protection before they reach the database. The API never returns a webhook address, only its host, because
Slack and Discord addresses are themselves credentials. A generic webhook's signing secret (`whsec_...`) is
returned once on create and on rotate. Requests are signed as `sha256=` plus the HMAC-SHA256 of the exact body.
The Data Protection keys live in the database (`data_protection_keys`), so the API and every worker read the same keys and a restart loses nothing.

### Outgoing requests get the same SSRF treatment as checks
Webhook sends use a dedicated client with no proxy and no redirects, a 10 second timeout, and the same
connect-time address guard as the check engine (`GuardedConnect`). A webhook that answers with a redirect is
treated as a failure rather than followed to somewhere the user never approved.

### Test alerts run inline
"Send test alert" sends immediately and returns the result instead of queueing, because the person is watching
and wants to know now. It is limited to 5 per minute per channel (429 `too_many_tests`). Test deliveries show in
the history like any other.

### Limits
20 channels per team, 10 email recipients per channel, 140 characters for an incident title and 2000 for a
message. Viewers read incidents and channels; Owners and Admins write.

### The dashboard is the summary endpoint
`GET /api/orgs/{id}/summary` returns the counts, open incidents and uptime for 24 hours, 7 and 30 days, all
aggregated in PostgreSQL, and `/series` returns the response-time series. Incident and status events from the
socket invalidate them, with a one minute poll as a safety net.
