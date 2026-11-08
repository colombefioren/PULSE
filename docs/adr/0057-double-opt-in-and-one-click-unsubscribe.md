# 57. Double opt-in and one-click unsubscribe

Status: Accepted

Milestone: M4: status pages

## Decision and rationale

An address counts only after the link in the first email is used, and that link works once. Every later email
has an unsubscribe link and `List-Unsubscribe` / `List-Unsubscribe-Post` headers, so Gmail and Apple Mail can
unsubscribe without opening anything; the POST is idempotent. Mail goes through the same outbox as alerts and is
built from the database at send time, so anyone who left in between is skipped. The cap is 5000 subscribers a page.
