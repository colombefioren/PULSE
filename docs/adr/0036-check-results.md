# 36. Check results

Status: Accepted

Milestone: M2 - Monitors and the check engine

## Decision and rationale

- A check passes when the status is in the expected list (default `200-299`) and, if set, the body contains the
  keyword. The keyword is case-sensitive and searched in the first 1 MB only; a HEAD monitor cannot have one.
- Failures name their cause in plain words (`Timeout after 10s - the server didn't respond`,
  `Expected 200-299, got 503`), because the message is what people read at 3 a.m.
- Thresholds: a monitor goes Down after `FailureThreshold` failures in a row and back Up after
  `RecoveryThreshold` passes in a row (defaults 3 and 2). The first result moves Pending straight to Up or to
  counting failures. Results that arrive for a paused monitor are ignored.
- Editing the address, method, status codes, keyword or headers starts the history over (state Pending), since
  old results describe a different check. Renaming or changing the interval does not.
