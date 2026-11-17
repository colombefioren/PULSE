# 64. A real demo that cannot hurt anyone

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

"Try the demo" signs in to a shared account on a real team that watches six well-known public websites, so the
visitor sees live data instead of a script. The account is a Viewer, its password is random and thrown away, and
the only way in is `POST /api/demo/session`, which is rate limited. It cannot create teams, which would otherwise
let any visitor spend the demo's storage. If the server has no demo (`Demo__Enabled` is false) the endpoint answers
404 and the web app falls back to the built-in sample workspace; someone already signed in is never switched to it.
