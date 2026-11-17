# 27. Rate limiting on the auth endpoints

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

A fixed window policy (`RateLimiting:AuthPermitsPerMinute`, default 20 per client address) covers the auth
endpoints and answers 429 with `Retry-After`. Behind nginx the API must trust `X-Forwarded-For`, otherwise every
caller looks like the proxy; compose enables `ASPNETCORE_FORWARDEDHEADERS_ENABLED`. Do that only where the API is
reachable through the proxy alone.
