# 5. Testcontainers 4.16

Status: Accepted

Milestone: M0 - Foundations

## Decision and rationale

Testcontainers 4.7 pulled in `SSH.NET` 2024.2.0, which has known high-severity advisories, and the build
treats warnings as errors. 4.16 does not, so it is used instead of suppressing the advisory.
