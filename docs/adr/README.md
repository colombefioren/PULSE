# Architecture decision records

Each file records one decision made while building Pulse, with the reasoning and the alternatives that were rejected. Records are numbered in the order they were taken and are not rewritten: a decision that changes gets a new record, and the old one is marked as superseded.

Pulse was built from a written brief. Most records cover a place where the brief was ambiguous or where reality differed from it.

| # | Decision | Milestone | Status |
| :-- | :-- | :-- | :-- |
| [0001](0001-net-10-and-the-slnx-solution-format.md) | .NET 10 and the `.slnx` solution format | M0 - Foundations | Accepted |
| [0002](0002-fluentassertions-stays-on-7-x.md) | FluentAssertions stays on 7.x | M0 - Foundations | Accepted |
| [0003](0003-typescript-6-0-not-7.md) | TypeScript 6.0, not 7 | M0 - Foundations | Accepted |
| [0004](0004-eslint-instead-of-the-template-s-oxlint.md) | ESLint instead of the template's oxlint | M0 - Foundations | Accepted |
| [0005](0005-testcontainers-4-16.md) | Testcontainers 4.16 | M0 - Foundations | Accepted |
| [0006](0006-brand-yellow-is-sampled-not-approximate.md) | Brand yellow is sampled, not approximate | M0 - Foundations | Accepted |
| [0007](0007-the-logo-is-traced-not-redrawn.md) | The logo is traced, not redrawn | M0 - Foundations | Accepted |
| [0008](0008-theme-is-a-data-theme-attribute-set-before-first-paint.md) | Theme is a `data-theme` attribute set before first paint | M0 - Foundations | Accepted |
| [0009](0009-deferred-shell-pieces.md) | Deferred shell pieces | M0 - Foundations | Accepted |
| [0010](0010-content-aligns-left.md) | Content aligns left | M0 - Foundations | Accepted |
| [0011](0011-logging-bootstrap.md) | Logging bootstrap | M0 - Foundations | Accepted |
| [0012](0012-ci-cd.md) | CI/CD | M0 - Foundations | Accepted |
| [0013](0013-repository-settings.md) | Repository settings | M0 - Foundations | Accepted |
| [0014](0014-commit-style.md) | Commit style | M0 - Foundations | Accepted |
| [0015](0015-visual-direction.md) | Visual direction | Design refresh (before M1) | Accepted |
| [0016](0016-palette-and-tokens.md) | Palette and tokens | Design refresh (before M1) | Accepted |
| [0017](0017-landing-page.md) | Landing page | Design refresh (before M1) | Accepted |
| [0018](0018-charts.md) | Charts | Design refresh (before M1) | Accepted |
| [0019](0019-the-public-status-page.md) | The public status page | Design refresh (before M1) | Accepted |
| [0020](0020-demo-backed-data-layer.md) | Demo-backed data layer | Design refresh (before M1) | Accepted |
| [0021](0021-accounts-were-not-open-yet.md) | Accounts were not open yet | Design refresh (before M1) | Superseded in M1 |
| [0022](0022-cookie-sign-in-not-tokens.md) | Cookie sign-in, not tokens | M1 - Accounts and organizations | Accepted |
| [0023](0023-antiforgery-for-every-write.md) | Antiforgery for every write | M1 - Accounts and organizations | Accepted |
| [0024](0024-sign-up-never-reveals-whether-an-address-exists.md) | Sign up never reveals whether an address exists | M1 - Accounts and organizations | Accepted |
| [0025](0025-email-must-be-verified-before-sign-in.md) | Email must be verified before sign in | M1 - Accounts and organizations | Accepted |
| [0026](0026-passwords.md) | Passwords | M1 - Accounts and organizations | Accepted |
| [0027](0027-rate-limiting-on-the-auth-endpoints.md) | Rate limiting on the auth endpoints | M1 - Accounts and organizations | Accepted |
| [0028](0028-invitations-replace-add-member.md) | Invitations replace "add member" | M1 - Accounts and organizations | Accepted |
| [0029](0029-tenant-isolation-answers-404.md) | Tenant isolation answers 404 | M1 - Accounts and organizations | Accepted |
| [0030](0030-roles-and-the-last-owner.md) | Roles and the last Owner | M1 - Accounts and organizations | Accepted |
| [0031](0031-migrations.md) | Migrations | M1 - Accounts and organizations | Accepted |
| [0032](0032-real-accounts-see-no-fake-data.md) | Real accounts see no fake data | M1 - Accounts and organizations | Accepted |
| [0033](0033-browser-security-headers.md) | Browser security headers | M1 - Accounts and organizations | Accepted |
| [0034](0034-the-entity-is-httpmonitor.md) | The entity is `HttpMonitor` | M2 - Monitors and the check engine | Accepted |
| [0035](0035-addresses-are-checked-three-times.md) | Addresses are checked three times | M2 - Monitors and the check engine | Accepted |
| [0036](0036-check-results.md) | Check results | M2 - Monitors and the check engine | Accepted |
| [0037](0037-scheduling-without-a-queue.md) | Scheduling without a queue | M2 - Monitors and the check engine | Accepted |
| [0038](0038-check-history-is-partitioned-by-month.md) | Check history is partitioned by month | M2 - Monitors and the check engine | Accepted |
| [0039](0039-concurrency.md) | Concurrency | M2 - Monitors and the check engine | Accepted |
| [0040](0040-live-updates-notify-to-signalr.md) | Live updates: NOTIFY to SignalR | M2 - Monitors and the check engine | Accepted |
| [0041](0041-api-shape.md) | API shape | M2 - Monitors and the check engine | Accepted |
| [0042](0042-demo-stays-read-only.md) | Demo stays read-only | M2 - Monitors and the check engine | Accepted |
| [0043](0043-an-incident-is-the-monitor-s-state-change-nothing-more.md) | An incident is the monitor's state change, nothing more | M3: incidents and alerts | Accepted |
| [0044](0044-alerts-go-through-an-outbox.md) | Alerts go through an outbox | M3: incidents and alerts | Accepted |
| [0045](0045-the-dispatcher-is-at-least-once.md) | The dispatcher is at-least-once | M3: incidents and alerts | Accepted |
| [0046](0046-secrets-are-encrypted-and-shown-once.md) | Secrets are encrypted and shown once | M3: incidents and alerts | Accepted |
| [0047](0047-outgoing-requests-get-the-same-ssrf-treatment-as-checks.md) | Outgoing requests get the same SSRF treatment as checks | M3: incidents and alerts | Accepted |
| [0048](0048-test-alerts-run-inline.md) | Test alerts run inline | M3: incidents and alerts | Accepted |
| [0049](0049-limits.md) | Limits | M3: incidents and alerts | Accepted |
| [0050](0050-the-dashboard-is-the-summary-endpoint.md) | The dashboard is the summary endpoint | M3: incidents and alerts | Accepted |
| [0051](0051-the-public-page-is-built-not-stored.md) | The public page is built, not stored | M4: status pages | Accepted |
| [0052](0052-monitor-incidents-are-rewritten-for-the-public.md) | Monitor incidents are rewritten for the public | M4: status pages | Accepted |
| [0053](0053-component-status.md) | Component status | M4: status pages | Accepted |
| [0054](0054-cached-for-30-seconds-evicted-on-change.md) | Cached for 30 seconds, evicted on change | M4: status pages | Accepted |
| [0055](0055-a-hub-that-only-says-changed.md) | A hub that only says "changed" | M4: status pages | Accepted |
| [0056](0056-public-endpoints.md) | Public endpoints | M4: status pages | Accepted |
| [0057](0057-double-opt-in-and-one-click-unsubscribe.md) | Double opt-in and one-click unsubscribe | M4: status pages | Accepted |
| [0058](0058-logos-live-in-the-database.md) | Logos live in the database | M4: status pages | Accepted |
| [0059](0059-addresses.md) | Addresses | M4: status pages | Accepted |
| [0060](0060-accent-colour.md) | Accent colour | M4: status pages | Accepted |
| [0061](0061-drop-partitions-never-delete-rows.md) | Drop partitions, never delete rows | M5: retention, badges, demo and launch | Accepted |
| [0062](0062-90-day-uptime-reads-two-sources.md) | 90-day uptime reads two sources | M5: retention, badges, demo and launch | Accepted |
| [0063](0063-the-badge-says-nothing-it-was-not-asked-to.md) | The badge says nothing it was not asked to | M5: retention, badges, demo and launch | Accepted |
| [0064](0064-a-real-demo-that-cannot-hurt-anyone.md) | A real demo that cannot hurt anyone | M5: retention, badges, demo and launch | Accepted |
| [0065](0065-why-the-command-palette-is-hand-built.md) | Why the command palette is hand-built | M5: retention, badges, demo and launch | Accepted |
| [0066](0066-end-to-end-tests-need-one-exception-to-the-network-rule.md) | End-to-end tests need one exception to the network rule | M5: retention, badges, demo and launch | Accepted |
| [0067](0067-lessons-from-running-the-real-stack.md) | Lessons from running the real stack | M5: retention, badges, demo and launch | Accepted |
