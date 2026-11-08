# 22. Cookie sign-in, not tokens

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

The API signs people in with an HTTP-only, `SameSite=Lax` cookie (`pulse.auth`, 14 days, sliding) through ASP.NET
Core Identity. Script on the page can never read it, which removes the usual place a stolen token comes from.
Unauthenticated and forbidden requests answer 401 and 403 instead of redirecting, because the client is an SPA.
