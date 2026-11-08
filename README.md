<p align="center">
  <img src="docs/brand/pulse-logo.png" alt="Pulse" width="420">
</p>

# Pulse

Pulse checks websites and APIs on a schedule, detects outages, alerts the team, and publishes a live public
status page. Monitoring and a status page in one place, built on .NET, React and PostgreSQL.

> Status: **v0.5.0, status pages.** Accounts, teams, monitoring, incidents, alerts and public status pages are
> real: when a monitor goes down Pulse opens an incident, tells your team by email, Slack, Discord or a signed
> webhook, and shows it on a page your customers can read and subscribe to. "Try the demo" shows the whole
> product on seeded data.

## Accounts

Create an account at `/signup`. In the local stack the confirmation email arrives in Mailpit
(http://localhost:8025). Every account belongs to teams with one of three roles:

| Role | Can |
| --- | --- |
| Owner | Everything, including delete the team and grant or revoke Owner |
| Admin | Manage members and invitations (not Owners) |
| Viewer | Look, nothing else |

People join a team through an emailed invitation that works for 7 days. A person who is not a member of a team
gets a 404 for anything inside it, so team ids cannot be probed.

## Monitors

Owners and Admins add monitors from **Monitors > Add monitor** (or press `C`). A monitor checks one `http` or
`https` address every 30 seconds to 30 minutes and can require specific status codes, a keyword in the response
and request headers. It goes Down after a few failures in a row and Up again after a few passes, so one dropped
packet does not page anyone. Each monitor shows uptime for 24 hours, 7 and 30 days, average and p95/p99 response
times, a response chart and the recent checks with the reason for every failure. Viewers can look but not change.

The checks run in `Pulse.Worker`, so the stack needs the worker as well as the API. Addresses that point at
private or internal networks (localhost, 10.x, 192.168.x, cloud metadata and so on) are refused when saving and
again when connecting, so Pulse cannot be used to probe your network. Monitoring a service on your own machine
therefore needs a public address, for example through a tunnel.

## Incidents and alerts

An incident opens by itself when a monitor goes Down and resolves when it comes back Up. Its timeline records
what the monitor saw, who acknowledged it and any notes your team adds. For things monitors cannot see, Owners
and Admins can **declare an incident** by hand and post status updates (Investigating, Identified, Monitoring,
Resolved) on it. Open incidents show on the dashboard next to live counts and uptime for 24 hours, 7 and 30 days.

**Alert channels** (Alerts page) say where a message goes. Add email addresses, a Slack or Discord webhook, or a
generic webhook, then choose which channels each monitor uses in its form. A generic webhook receives a JSON
`POST` signed with a secret that is shown once: compare `X-Pulse-Signature` with `sha256=` plus the hex HMAC-SHA256
of the raw body. **Send test alert** checks a channel end to end, and each channel keeps its delivery history.
Failed deliveries are retried after 1, 5, 15 and 60 minutes before they are marked failed.

## Status pages

Owners and Admins build up to five pages under **Status pages**. A page has a title, an address
(`/status/your-address`), an accent colour, a footer, an optional logo and a list of **components**: lines such as
"Website" or "API", each fed by one or more monitors and optionally grouped. A component is down when all its
monitors are down, degraded when only some are, and in maintenance when every monitor behind it is paused.

Visitors need no account. The public page shows the current status, 90 days of uptime per component, active
incidents with their updates and the last 30 days of past incidents, and it refreshes itself over a socket when
something changes. It is cached for 30 seconds and its endpoints are rate limited per address.

What visitors see is deliberately plain. An incident opened by a monitor reads "Issue affecting Checkout": the
monitor's name, address and error stay private, and so do internal notes and acknowledgements. Incidents you
declare by hand can name the components they affect and go out exactly as you wrote them.

Visitors can **subscribe by email**. They confirm from a link in the first email (nothing is sent before that),
and every later email carries a one-click unsubscribe link and the `List-Unsubscribe` headers mail apps use.
Subscribers hear when an incident on the page opens, gets a public update or resolves.

## Run it locally

```bash
git clone https://github.com/colombefioren/PULSE.git
cd PULSE
docker compose up --build
```

| Service | URL |
| --- | --- |
| Web | http://localhost:8080 |
| API | http://localhost:5080 (health: `/health/live`, `/health/ready`) |
| Mailpit | http://localhost:8025 |

Every setting has a development default. Copy `.env.example` to `.env` to change ports or credentials.

### Without Docker

Requirements: .NET 10 SDK, Node 24, PostgreSQL 16 and an SMTP catcher such as Mailpit on port 1025. The
Development settings expect the `pulse` / `pulse_dev_password` database from `.env.example` on `localhost:5432`
and apply migrations on startup. Start only the dependencies with `docker compose up postgres mailpit`.

```bash
dotnet run --project src/Pulse.Api        # http://localhost:5080, OpenAPI UI at /scalar/v1
dotnet run --project src/Pulse.Worker
cd web && npm ci && npm run dev           # http://localhost:5173, proxies /api to the API
```

## Tests

```bash
dotnet test Pulse.slnx                    # unit and integration; integration tests start PostgreSQL with Testcontainers
cd web && npm run lint && npm run typecheck && npm test
```

Without Docker, point the integration tests at any PostgreSQL server you can create databases on. They create and
drop a scratch database of their own:

```bash
PULSE_TEST_POSTGRES="Host=127.0.0.1;Port=5432;Username=postgres;Password=..." dotnet test Pulse.slnx
```

Schema changes need a migration: `dotnet ef migrations add <Name> --project src/Pulse.Infrastructure --startup-project src/Pulse.Api`
(the tool is pinned in `dotnet-tools.json`; run `dotnet tool restore` once).

## Stack

| Layer | Choice |
| --- | --- |
| Backend | .NET 10, ASP.NET Core, Clean Architecture |
| Data | PostgreSQL 16, EF Core with Npgsql |
| Background work | `BackgroundService` with `System.Threading.Channels` |
| Real time | SignalR, worker events through `LISTEN/NOTIFY` |
| Frontend | React, TypeScript (strict), Vite, Tailwind CSS, Radix UI, TanStack Query |
| Tests | xUnit, FluentAssertions, Testcontainers, Vitest, Testing Library, Playwright |
| Delivery | Docker Compose, GitHub Actions |

## Milestones

| Version | Milestone | Status |
| --- | --- | --- |
| v0.1.0 | M0 Foundations | released |
| v0.2.0 | M1 Accounts and organizations | released |
| v0.3.0 | M2 Monitors and the check engine | released |
| v0.4.0 | M3 Incidents and alerts | released |
| v0.5.0 | M4 Status pages | released |
| v1.0.0 | M5 Polish and launch | planned |

## Branches

`preprod` is the default and integration branch. `prod` only receives promotion merge commits from `preprod`,
each tagged `vX.Y.Z` and recorded in [CHANGELOG.md](CHANGELOG.md). Work of five commits or fewer lands directly
on `preprod`; larger work goes through a short-lived `feat/*` branch and a pull request.

## More

- [Architecture](docs/architecture.md)
- [Decisions](docs/decisions.md)
