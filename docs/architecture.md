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

## What exists today (v0.1.0)

- The solution structure and project references above.
- API: structured JSON logging, correlation ids (`X-Correlation-Id`), `/health/live`, `/health/ready`, OpenAPI.
- Worker: host and structured logging with a placeholder service.
- Web: design system, logo, app shell and routes.
- Docker Compose and the CI/CD workflows.

Everything else in the flow is built in the milestones listed in the README.

## Frontend

- Tokens live in `web/src/index.css` as CSS variables; Tailwind reads them through `@theme`.
- Pages are loaded on demand per route to keep the initial bundle small.
- Theme: `data-theme` on `<html>`, set before first paint by `index.html`.
- Logo: `web/src/assets/logo-paths.ts` holds the traced path data used by the `Logo` and `LogoMark` components.
