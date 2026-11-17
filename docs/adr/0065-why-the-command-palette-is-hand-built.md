# 65. Why the command palette is hand-built

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

It needs a list, a filter and keyboard handling. A library for that would add more weight than the couple of hundred lines it
saves, and the initial bundle has a 250 KB budget. Matching is "every word appears somewhere", names that start with
the first word come first, and the whole thing is a Radix dialog so focus, Escape and screen readers already work.
