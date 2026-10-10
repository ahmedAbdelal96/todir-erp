# ADR-010 — Module Go-Live Lifecycle

Status: Accepted

## Decision

Lifecycle transitions are explicit, validated against dependency and entitlement state, audited, and concurrency protected. Kernel modules that cannot be disabled declare that policy explicitly.

## Consequences

Activation and deactivation are controlled state transitions rather than feature-flag side effects, and historical data is not deleted by deactivation.

## Evidence

`backend/Core/Modules/ModuleLifecycleService.cs` and lifecycle tests.
