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
