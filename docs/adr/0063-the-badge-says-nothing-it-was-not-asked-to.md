# 63. The badge says nothing it was not asked to

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

A badge is public, so it is opt-in per monitor and shows one number. "Switched off" and "no such monitor" return the
same 404 image, so ids cannot be probed. The result is cached for five minutes in memory and by browsers and
proxies, and shares the public read rate limit. Colours are chosen so white text keeps at least 4.5:1 contrast.
