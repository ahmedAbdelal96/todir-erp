# ADR-017 — Custom Fields Storage Boundary

Status: Accepted

## Decision

The kernel owns metadata definitions, validation shape and tenant scope for custom fields. Business modules own the entities and decide where validated values are projected; arbitrary executable scripting is forbidden.

## Consequences

Custom metadata remains extensible without turning the kernel into a universal business data store or execution engine.

## Evidence

`backend/Core/CustomFields`, schema policy, and custom-field tests.
