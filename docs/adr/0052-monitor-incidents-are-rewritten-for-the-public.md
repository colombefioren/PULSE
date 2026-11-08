# 52. Monitor incidents are rewritten for the public

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

Anything a monitor knows is internal: its name, the failing address, the last error. The page shows "Issue
affecting Checkout" instead, with a fixed sentence for the opening and the resolution. Notes and acknowledgements
are never shown. A manual incident was written for customers, so it goes out as typed. This is enforced in one
place (`PublicIncidentText`) and tested by searching the whole response for the secrets.
