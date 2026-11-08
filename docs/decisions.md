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

### Accounts are not open yet
The sign in and sign up forms validate inline, but submitting shows "Accounts are not open yet" with a link
to the demo workspace. Faking a successful login would mislead; real accounts arrive in M1.
