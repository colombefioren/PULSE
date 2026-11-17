# 24. Sign up never reveals whether an address exists

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

Register, forgot-password and resend-verification always answer the same way (202). If the address is already
registered, the email says so instead of the response. This costs a little friendliness and closes account
enumeration.
