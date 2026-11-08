# 31. Migrations

Status: Accepted

Milestone: M1 - Accounts and organizations

## Decision and rationale

Development and the integration tests apply migrations on startup (`Database:MigrateOnStartup`). Deployed
environments leave it off and run `dotnet ef database update` from the gated CI job, so a rolling API restart
never races a schema change. Data Protection keys are stored in the database so sign-in cookies and email tokens
survive restarts and work across instances.
