# 66. End-to-end tests need one exception to the network rule

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

Checks refuse private addresses, which is exactly where a test site in a container lives. `Checks__AllowPrivateTargets`
lifts the rule in the API and the worker, is false by default, and is set only in `docker-compose.e2e.yml`.
