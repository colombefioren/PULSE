# Changelog

All notable changes to Pulse are recorded here. The release workflow reads the version from the first
`## [x.y.z]` heading, so every promotion from `preprod` to `prod` adds an entry at the top.

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
