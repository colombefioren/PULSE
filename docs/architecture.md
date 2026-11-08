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
3. The incident evaluator updates monitor state. After N consecutive failures it opens an incident, and after
   M consecutive successes it resolves it.
4. State changes write an outbox row in the same transaction. The alert dispatcher reads the outbox and sends
   email and webhooks with retries.
5. The worker emits `NOTIFY pulse_events`. The API listens and pushes updates to clients over SignalR.
6. A nightly rollup aggregates raw results into hourly and daily summaries and drops raw partitions past the
   retention window.

## What exists today (v0.3.0)

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
- Live updates: the recorder `NOTIFY`s, each API instance `LISTEN`s and pushes to a SignalR group per team
  (`/hubs/live`); the web app patches its cache from the events and falls back to slow polling.
- Web: design system, logo, app shell, real sign in, team management and monitors, and a seeded demo workspace.
- Docker Compose and the CI/CD workflows.

Steps 1, 2 and 5 of the flow above are real. Incidents, the outbox and alerts (3 and 4) arrive in M3 and the
rollups (6) in M5. Today a monitor's state changes in the same save that records the result.

## Frontend

- Tokens live in `web/src/index.css` as CSS variables; Tailwind reads them through `@theme`.
- Pages are loaded on demand per route to keep the initial bundle small.
- Theme: `data-theme` on `<html>`, set before first paint by `public/theme-init.js`.
- Session: `session/useSession.ts` merges the signed-in user (`GET /api/auth/me`) with the demo flag and the
  chosen team. `RequireSession` guards the app shell; `api/client.ts` adds the antiforgery header to writes.
- Monitors: `data/monitors.ts` serves the Monitors screens from the API for a real account and from the
  simulation for the demo, both as the same `MonitorView`. `live/` opens the SignalR connection (lazy) and
  patches the TanStack Query cache from events, in a pure function that is unit tested.
- Demo: `data/queries.ts` is the seam to the API for the screens that are not built yet. Until a feature has a real endpoint, real accounts see a
  placeholder (`PendingFeature`) and only the demo reads seeded data.
- Logo: `web/src/assets/logo-paths.ts` holds the traced path data used by the `Logo` and `LogoMark` components.
