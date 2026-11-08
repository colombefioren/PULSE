# Changelog

All notable changes to Pulse are recorded here. The release workflow reads the version from the first
`## [x.y.z]` heading, so every promotion from `preprod` to `prod` adds an entry at the top.

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
