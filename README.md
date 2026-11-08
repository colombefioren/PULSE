<p align="center">
  <img src="docs/brand/pulse-logo.png" alt="Pulse" width="420">
</p>

# Pulse

Pulse checks websites and APIs on a schedule, detects outages, alerts the team, and publishes a live public
status page. Monitoring and a status page in one place, built on .NET, React and PostgreSQL.

> Status: **v0.1.0, foundations.** The solution, design system, app shell, Docker stack and CI/CD are in place.
> Monitoring, incidents, alerts and status pages land in the next milestones.

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

Requirements: .NET 10 SDK, Node 24, and a PostgreSQL 16 instance for the parts that need it.

```bash
dotnet run --project src/Pulse.Api        # http://localhost:5080, OpenAPI UI at /scalar/v1
dotnet run --project src/Pulse.Worker
cd web && npm ci && npm run dev           # http://localhost:5173, proxies /api to the API
```

## Tests

```bash
dotnet test Pulse.slnx                    # unit and integration (database tests use Docker from M1)
cd web && npm run lint && npm run typecheck && npm test
```

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
| v0.1.0 | M0 Foundations | in progress |
| v0.2.0 | M1 Accounts and organizations | planned |
| v0.3.0 | M2 Monitors and the check engine | planned |
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
