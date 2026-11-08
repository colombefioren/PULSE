# 48. Test alerts run inline

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

"Send test alert" sends immediately and returns the result instead of queueing, because the person is watching
and wants to know now. It is limited to 5 per minute per channel (429 `too_many_tests`). Test deliveries show in
the history like any other.
