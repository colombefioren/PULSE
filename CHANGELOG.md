# Changelog

All notable changes to Pulse are recorded here. The release workflow reads the version from the first
`## [x.y.z]` heading, so every promotion from `preprod` to `prod` adds an entry at the top.

## [1.0.0] - 2026-11-08

Polish and launch (milestone M5).

### Added
- Retention that never runs a `DELETE` over check results: the worker sums each month into hourly and daily
  rollups and then drops the whole monthly partition once all of it is older than 30 days. Hourly rollups are kept
  for a year and daily rollups forever.
- 90-day uptime on every monitor, read from the daily totals plus the raw checks of the last two days, so it is right
  whether or not the rollup job has run since.
- A public uptime badge, `/badge/{monitorId}.svg`: a shields-style image of the last 30 days, cached for five
  minutes, rate limited and off until a team switches it on for that monitor. A switched-off monitor and an unknown id
  get the same response. The monitor page shows the badge and the Markdown to paste.
- A shared, read-only demo account. With `Demo__Enabled=true`, "Try the demo" signs a visitor in to a real team that
  watches well-known public websites; without it the button opens the built-in sample workspace as before. The demo
  account is a Viewer, cannot sign in with a password and cannot create teams.
- A command palette on Ctrl+K (Cmd+K on a Mac) and in the top bar: jump to any monitor or page, create a monitor,
  switch team and change the theme.
- End-to-end tests with Playwright against the real stack in Docker: sign up and add a monitor, an outage that opens an
  incident the status page shows, recovery, and the badge. They run in CI.
- `Checks__AllowPrivateTargets`, off by default, for the end-to-end stack whose monitored site is a container.
- README with a pitch, screenshots, an architecture diagram and the design decisions.

### Fixed
- The worker could not start in the Development environment: the container check found that it could not build the
  account services without the authentication services, and that the alert dispatcher held a scoped transport for its
  whole life. A test now builds the worker's container with every registration checked.

## [0.5.0] - 2026-11-08

Status pages (milestone M4).

### Added
- Status pages: a team builds up to 5, each at `/status/{address}` with a title, an accent colour, a footer, an
  optional logo (PNG, JPEG or WebP up to 256 KB) and a switch for average response times.
- Components: lines on the page fed by one or more monitors, optionally grouped and reordered. A component is
  down when all its monitors are down and degraded when only some are; paused monitors do not count.
- The public page: current status banner, 90-day uptime bars per component, active incidents with their public
  updates and the last 30 days of past incidents. It needs no sign-in, is cached for 30 seconds and refreshes
  itself over a socket the moment something changes.
- Manual incidents can name the components they affect, so they show on the page with the text the team wrote.
  Incidents opened by monitors show a plain sentence without the monitor's name, address or error.
- Email subscribers: anyone can ask for updates, confirm by email (double opt-in) and leave with one click, including
  the one-click unsubscribe header mail apps use. Subscribers hear when an incident on the page opens, gets a
  public update or resolves.
- A daily rollup job in the worker so the 90-day bars read compact per-day numbers instead of raw checks.
- Rate limits on the public endpoints: 120 reads and 6 subscribe or unsubscribe requests per minute per address.
- Status pages and a component picker when declaring an incident in the web app; a page editor with logo upload,
  component ordering and a live contrast hint for the accent colour.

### Changed
- `/status-pages` shows real pages for signed-in accounts instead of a placeholder.
- The public page for real addresses is drawn from the API; `/status/pulse-demo` stays a simulation.

## [0.4.0] - 2026-11-08

Incidents and alerts (milestone M3).

### Added
- Incidents that open when a monitor goes Down and resolve when it comes back Up, with a timeline of what the
  monitor saw, acknowledgement, internal notes and the alerts that were sent.
- Manual incidents with a severity and public status updates (Investigating, Identified, Monitoring, Resolved).
- Alert channels: email, Slack, Discord and a generic webhook signed with HMAC-SHA256 (`X-Pulse-Signature`),
  chosen per monitor, with a "Send test alert" button, delivery history and webhook secret rotation.
- An outbox and dispatcher in the worker: alerts are staged in the same transaction as the check result,
  sent at least once, and retried after 1, 5, 15 and 60 minutes before being marked failed.
- Channel settings encrypted at rest; webhook addresses are never shown after they are saved.
- A real dashboard: monitor counts, open incidents, uptime for 24 hours, 7 and 30 days and a team-wide response
  time chart, updated live.
- Incidents list and detail screens, an Alerts screen, alert channel choices in the monitor form and an
  Incidents tab on each monitor.

### Changed
- `/dashboard`, `/incidents` and `/alerts` show real data for signed-in accounts instead of a placeholder.
- Outgoing webhook requests share the connect-time address guard used by the check engine.

## [0.3.0] - 2026-11-08

Monitors and the check engine (milestone M2).

### Added
- Monitors: add, edit, pause, resume and delete an HTTP or HTTPS check with a method, interval (30 s to 30 min),
  timeout, expected status codes, a keyword the response must contain and request headers. Up to 50 per team.
- A check engine in the worker: a scheduler that claims due monitors without a queue (`FOR UPDATE SKIP LOCKED`),
  jittered intervals, a bounded pool of runners and a recorder that stores each result and any state change
  together. Several workers can run side by side.
- Failure thresholds: a monitor goes Down after N failed checks in a row and Up after M passes in a row.
- Plain-language failure reasons: timeouts, DNS and TLS errors, refused connections, unexpected status codes and
  missing keywords.
- Stats per monitor: uptime for 24 hours, 7 and 30 days, average, p50, p95 and p99 response time and a chart series.
- Check history in monthly partitions, created ahead of time by the worker.
- Live updates over SignalR, fed by PostgreSQL `NOTIFY`, so statuses and recent checks change on screen without
  reloading. The connection is authenticated and joined per team, with a membership check.
- Monitors list and detail pages on real data, an add and edit form with server-side error messages, a delete
  confirmation and the `C` shortcut. Viewers see everything and change nothing.

### Security
- Monitor addresses are validated on save and again when the connection is made, so names that resolve to
  private, loopback, link-local or metadata addresses (including through redirects and DNS changes) are never
  contacted.
- Request header values are masked for Viewers.

### Changed
- A signed-in account lands on Monitors; the dashboard stays a demo screen until incidents arrive.

## [0.2.0] - 2026-11-08

Accounts and organizations (milestone M1).

### Added
- Sign up with email verification, sign in, sign out and password reset, with enumeration-safe responses, a
  lockout after repeated failures and rate limiting on the auth endpoints.
- HTTP-only cookie sign-in with antiforgery tokens on every write.
- Teams with Owner, Admin and Viewer roles, a last-Owner rule, and team rename and delete.
- Emailed invitations (hashed token, 7-day expiry) that replace adding a member by address.
- Tenant isolation: people outside a team get 404 for everything in it, covered by integration tests that run
  against real PostgreSQL.
- EF Core persistence with the first migration, a database-aware readiness check and a gated CI migrate step.
- Sign in, sign up, verify email, forgot and reset password and invitation pages; a team switcher in the sidebar;
  team settings with members, roles, invitations and a delete confirmation.
- "Try the demo" at `/demo`, which keeps the seeded workspace available without an account.
- Content security policy and other security headers on the web app.

### Changed
- Pages that are not built yet tell a real account so, instead of showing the demo's data.
- The inline theme script moved to `theme-init.js` so scripts can be restricted to the app's own origin.

## [0.1.0] - 2026-11-08

Foundations (milestone M0) and the first version of the interface, running on a seeded demo workspace.

### Added
- Clean Architecture .NET 10 solution: Domain, Application, Infrastructure, Api and Worker projects.
- API with structured JSON logs, a correlation id per request, `/health/live`, `/health/ready` and
  OpenAPI with a Scalar UI in development.
- Worker host with structured JSON logs.
- UUID v7 id generation in the domain.
- React, TypeScript (strict) and Vite app with Tailwind, React Router and TanStack Query.
- Design tokens for light and dark themes, status colors and Geist type.
- The Pulse logo and icon traced as SVG from the brand PNG, with a once-only heartbeat draw-in.
- App shell: collapsible sidebar, mobile drawer, breadcrumbs, theme toggle, `G then M` and `G then I`.
- Docker Compose stack with PostgreSQL 16, Mailpit, API, worker and web.
- GitHub Actions: CI (build, format check, tests, lint, typecheck, image builds), preprod publish,
  prod release with tagging, and Dependabot.
- Dashboard with live counts, open incidents you can acknowledge, a response-time graph and three uptime gauges.
- Public status page with a banner, grouped components, 90-day uptime bars, incident timelines, a response-time graph and an email subscribe form.
- Monitor detail page with uptime figures and a response-time chart.
- Sign in and sign up pages with inline validation.
- Landing page with a live product preview, feature cards and the live status page embedded.
- A seeded demo workspace behind query hooks, so the UI works before the API exists.

### Changed
- Restyled the app as a floating sidebar, rounded shell and ring-gauge rail, in the original yellow palette, with pill controls.
