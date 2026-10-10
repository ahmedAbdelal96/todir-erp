# ADR-001 — Modular Monolith

Status: Accepted

## Decision

Keep one repository and one deployable modular monolith. Modules own their contracts, application logic, persistence and migrations; cross-module calls use approved contracts and the dependency graph remains acyclic.

## Consequences

Deployment and transactions stay simple while module boundaries are enforced by architecture tests. Independent services are not introduced without a later architecture decision.

## Evidence

`backend/Tests/Architecture` and `docs/architecture/solution-boundaries.md`.
