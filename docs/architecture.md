# Architecture

A .NET API and a separate .NET worker share one PostgreSQL database. A React app talks to the API over
REST and SignalR.

```
                 +-------------------+
   browser ----> |  web (React, nginx)|
                 +---------+---------+
                           | /api  /hubs  /badge
                 +---------v---------+        LISTEN pulse_events
                 |   Pulse.Api       | <----------------------------+
                 |  REST + SignalR   |                              |
                 +---------+---------+                              |
                           |                                        |
                 +---------v----------------------------------------+--+
                 |                  PostgreSQL 16                      |
                 +---------^----------------------------------------+--+
                           |                                        |
                 +---------+---------+        NOTIFY pulse_events   |
                 |   Pulse.Worker    | -----------------------------+
                 | scheduler, runners|
                 | rollups, alerts   |
                 +-------------------+
```

## Projects

| Project | Role | Depends on |
| --- | --- | --- |
| `Pulse.Domain` | Entities, value objects and domain rules. No dependencies. | none |
| `Pulse.Application` | Use cases, interfaces, DTOs and validators. | Domain |
| `Pulse.Infrastructure` | EF Core, PostgreSQL, email and the HTTP checker. | Application |
| `Pulse.Api` | REST endpoints, SignalR hub, auth, health. | Application, Infrastructure |
| `Pulse.Worker` | Scheduler, check runners, rollups, alert dispatcher. | Application, Infrastructure |
| `web` | React single-page app. | API over HTTP |

Dependencies point inward: Domain knows nothing, and nothing in Domain or Application references
Infrastructure.

## Runtime flow (target design)

1. The scheduler wakes every second, selects monitors with `next_check_at <= now()` using
   `FOR UPDATE SKIP LOCKED`, and pushes them into a bounded channel.
2. A fixed pool of check runners consumes the channel, makes the HTTP request with a timeout and writes a
   `check_result`.
3. The incident workflow runs in the same save as the result. When a monitor goes Down it opens an incident, and
   when it comes back Up it resolves it.
4. The same save stages one delivery and one outbox row per alert channel. The alert dispatcher claims due rows
   with `FOR UPDATE SKIP LOCKED`, sends outside the transaction and retries with back-off.
5. The worker emits `NOTIFY pulse_events`. The API listens and pushes updates to clients over SignalR.
6. A rollup job in the worker sums raw results into one row per monitor per UTC day (yesterday and today every
   ten minutes, a 35 day backfill at start). Hourly rollups and dropping old raw partitions arrive in M5.
7. A visitor of a public status page asks the API for `/api/public/status/{slug}`. The API builds the page from
   the database (rollups for older days, raw results for yesterday and today), keeps it in memory for 30 seconds
   and drops the copy when a live event says something changed. Open pages learn that through the anonymous
   `/hubs/status` hub, which only says "changed" and never carries data.
8. An incident change on a page's component stages one `subscriber.notify` outbox row per confirmed subscriber in
   the same save. The dispatcher sends each as an email built from the database at send time, so someone who
   unsubscribed in between gets nothing.

## What exists today (v0.5.0)

- The solution structure and project references above.
- API: structured JSON logging, correlation ids (`X-Correlation-Id`), `/health/live`, `/health/ready` (checks
  the database), OpenAPI.
- Accounts: sign up, email verification, sign in, sign out, password reset (ASP.NET Core Identity, cookie
  auth, antiforgery, rate limiting).
- Organizations: teams, memberships with Owner / Admin / Viewer roles, emailed invitations, tenant isolation
  tests. All routes live under `/api`.
- Persistence: EF Core with Npgsql, snake_case naming, migrations in `Pulse.Infrastructure`.
- Monitors: create, edit, pause, resume and delete; stats (uptime, average, p50/p95/p99, series) computed in the
  database; check history in monthly partitions.
- Check engine (`Pulse.Worker`): claiming scheduler (`FOR UPDATE SKIP LOCKED`), bounded channel, a pool of HTTP
  runners with connect-time SSRF protection, and a recorder that stores the result and the state change in one
  save. A maintenance service keeps future partitions created.
- Incidents: opened and resolved by the incident workflow in the recorder's transaction, with a timeline,
  acknowledgement, notes and manual incidents (`Pulse.Application/Incidents`).
- Alerts: channels (email, Discord, Slack, signed webhook) whose settings are encrypted with Data Protection,
  a delivery per channel and incident change, and the outbox dispatcher in the worker
  (`Pulse.Infrastructure/Alerts`).
- Dashboard summary: counts, uptime and the response series, computed in the database.
- Live updates: the recorder `NOTIFY`s, each API instance `LISTEN`s and pushes to a SignalR group per team
  (`/hubs/live`); the web app patches its cache from the events and falls back to slow polling.
- Web: design system, logo, app shell, real sign in, team management and monitors, and a seeded demo workspace.
- Status pages: pages, components and a logo stored in the database (`Pulse.Application/StatusPages`), the
  public page assembled by `PublicStatusService`, `StatusCache`, the anonymous `StatusHub`, public endpoints with
  per-address rate limits, double opt-in subscribers and the subscriber mailer.
- Daily rollups (`monitor_daily_stats`) written by the worker.
- Docker Compose and the CI/CD workflows.

Steps 1 to 5 of the flow above are real. The rollups (6) arrive in M5.

## Frontend

- Tokens live in `web/src/index.css` as CSS variables; Tailwind reads them through `@theme`.
- Pages are loaded on demand per route to keep the initial bundle small.
- Theme: `data-theme` on `<html>`, set before first paint by `public/theme-init.js`.
- Session: `session/useSession.ts` merges the signed-in user (`GET /api/auth/me`) with the demo flag and the
  chosen team. `RequireSession` guards the app shell; `api/client.ts` adds the antiforgery header to writes.
- Monitors: `data/monitors.ts` serves the Monitors screens from the API for a real account and from the
  simulation for the demo, both as the same `MonitorView`. `live/` opens the SignalR connection (lazy) and
  patches the TanStack Query cache from events, in a pure function that is unit tested.
- Incidents and alerts: `data/incidents.ts` and `data/alerts.ts` follow the same pattern (real accounts read the
  API, the demo reads the simulation, screens render one view type). Incident events refresh the lists and the
  dashboard counts.
- Demo: `data/queries.ts` is the seam to the API for the screens that are not built yet. Until a feature has a real endpoint, real accounts see a
  placeholder (`PendingFeature`) and only the demo reads seeded data.
- Logo: `web/src/assets/logo-paths.ts` holds the traced path data used by the `Logo` and `LogoMark` components.
