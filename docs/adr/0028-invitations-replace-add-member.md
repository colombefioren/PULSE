# 28. Invitations replace "add member"

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

The brief lists `POST /orgs/{id}/members`. Adding a person who has not agreed, by address alone, would let anyone
attach a stranger to a team. Invitations are used instead: a 32-byte random token is emailed, only its SHA-256 is
stored, it expires after 7 days, and a second invitation to the same address replaces the first. Accepting needs
a signed-in account whose email matches the invitation.
