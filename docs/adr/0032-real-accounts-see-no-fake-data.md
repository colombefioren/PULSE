# 32. Real accounts see no fake data

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

Incidents, status pages and the dashboard still read the seeded workspace, which belongs to the demo (monitors became real in M2).
A real account sees a plain "arrives in an upcoming release" panel on those pages instead of someone else's
numbers. "Try the demo" (`/demo`) switches to the seeded workspace for the tab and signing in switches it off.
