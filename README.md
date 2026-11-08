<p align="center">
  <img src="docs/brand/pulse-logo.png" alt="Pulse" width="420">
</p>

# Pulse

Pulse checks websites and APIs on a schedule, detects outages, alerts the team, and publishes a live public
status page. Monitoring and a status page in one place, built on .NET, React and PostgreSQL.

> Status: **v0.3.0, monitors.** Accounts, teams and roles are real, and so is monitoring: add a URL and the
> worker checks it on schedule, records every result and updates the screen live. Incidents, alerts and status
> pages land in the next milestones; until then, "Try the demo" shows the whole product on seeded data.

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
| v0.3.0 | M2 Monitors and the check engine | in progress |
| v0.4.0 | M3 Incidents and alerts | planned |
| v0.5.0 | M4 Status pages | planned |
| v1.0.0 | M5 Polish and launch | planned |

## Branches

`preprod` is the default and integration branch. `prod` only receives promotion merge commits from `preprod`,
each tagged `vX.Y.Z` and recorded in [CHANGELOG.md](CHANGELOG.md). Work of five commits or fewer lands directly
on `preprod`; larger work goes through a short-lived `feat/*` branch and a pull request.

## More

- [Architecture](docs/architecture.md)
- [Decisions](docs/decisions.md)
