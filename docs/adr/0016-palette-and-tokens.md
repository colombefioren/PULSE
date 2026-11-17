# 16. Palette and tokens

Status: Accepted

Milestone: Design refresh (before M1)

## Decision and rationale

Light: canvas `#e8e6e0`, shell `#f8f7f5`, cards `#ffffff`, ink `#0b0b0c`. The sidebar and the gauge rail are near-black in
both themes so the yellow logo always has contrast. Everything is a CSS variable with a dark override, so
components never hold a hex.
