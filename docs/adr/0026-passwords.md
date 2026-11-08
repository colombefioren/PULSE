# 26. Passwords

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

Minimum length 10, no character-class rules (length beats complexity), 5 failed attempts lock the account for 5
minutes. Hashing is Identity's PBKDF2 default.
