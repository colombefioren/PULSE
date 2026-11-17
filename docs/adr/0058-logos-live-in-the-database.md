# 58. Logos live in the database

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

A logo is at most 256 KB, so a table column is simpler than object storage and is backed up with everything else.
Only PNG, JPEG and WebP are accepted, the first bytes must match the claimed type, and SVG is refused because it
can carry script and the image is served from our own origin. The address carries a version so browsers can
cache it for a year and still see a replacement at once.
