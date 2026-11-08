# 46. Secrets are encrypted and shown once

Status: Accepted

Milestone: M3: incidents and alerts

## Decision and rationale

A channel's settings (recipients, webhook address, signing secret) are JSON encrypted with ASP.NET Data
Protection before they reach the database. The API never returns a webhook address, only its host, because
Slack and Discord addresses are themselves credentials. A generic webhook's signing secret (`whsec_...`) is
returned once on create and on rotate. Requests are signed as `sha256=` plus the HMAC-SHA256 of the exact body.
The Data Protection keys live in the database (`data_protection_keys`), so the API and every worker read the same keys and a restart loses nothing.
