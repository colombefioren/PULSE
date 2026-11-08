# 8. Theme is a `data-theme` attribute set before first paint

Status: Accepted

Milestone: M0 - Foundations

## Decision and rationale

An inline script in `index.html` reads the stored choice, falls back to the system setting, and sets
`data-theme` before React loads, so there is no flash. The toggle cycles system, light, dark.
