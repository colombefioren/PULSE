# 33. Browser security headers

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

nginx serves the app with a strict content security policy (own origin only, no inline scripts, which is why the
theme bootstrap is `theme-init.js`), `nosniff`, no framing, a restrictive referrer policy and HSTS. The API sends
its own policy that allows nothing, since it only returns JSON.
