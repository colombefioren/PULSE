# 56. Public endpoints

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

No session, so no antiforgery token: `/api/public` is exempt from the CSRF check and protected by rate limits
instead (120 reads and 6 writes per minute per address). Subscribe always answers 202 whether or not the
address is already subscribed, so the form cannot reveal who follows a page. Confirm and unsubscribe links redirect
to the site with `?subscription=confirmed|removed`, or to `/subscription/invalid` for an unknown or used link.
