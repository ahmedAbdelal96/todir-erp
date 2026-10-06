# ADR-014 — Time and Date Policy

Status: Accepted

## Decision

Persist instants as UTC timestamps, inject `TimeProvider` into time-sensitive services, and use explicit date/period semantics for business boundaries. Local machine time is not domain truth.

## Consequences

Expiry, scheduling, MFA and support-access behavior can be tested deterministically without sleeping in tests.

## Evidence

Platform services, injected clock tests, and the scheduling/support-access implementations.
