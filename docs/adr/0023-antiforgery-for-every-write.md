# 23. Antiforgery for every write

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

Because the browser sends the cookie automatically, every non-GET request must also carry `X-XSRF-TOKEN`.
`GET /api/auth/csrf` returns the token; the client keeps it in memory and refetches after sign in or out, since
tokens are bound to the signed-in identity. A stale token gets one transparent retry.
