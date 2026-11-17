# 25. Email must be verified before sign in

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

A correct password for an unverified address answers 403 `email_not_verified`, and the sign in page offers to
resend the link. The check runs after the password check so it does not become an enumeration oracle either.
