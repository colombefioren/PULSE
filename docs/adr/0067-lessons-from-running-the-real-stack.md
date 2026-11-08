# 67. Lessons from running the real stack

Status: Accepted

Milestone: M5: retention, badges, demo and launch

## Decision and rationale

The first run of the worker in the Development environment failed: container validation found a singleton that held
a scoped service, and account services that could not be built without the authentication services. Neither showed
in the unit or integration tests, because those never build the worker's container. `CompositionTests` does now.
