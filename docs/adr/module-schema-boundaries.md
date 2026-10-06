# ADR-004 — Schema and DbContext per Module

Status: Accepted

## Decision

Each module owns its schema, tables, migration scripts and data access boundary. Modules do not directly read or write another module's tables; shared kernel types remain framework-neutral.

## Consequences

Cross-module invariants use contracts and the approved transaction coordinator instead of a global data context.

## Evidence

`docs/architecture/solution-boundaries.md`, module manifests, SQL ownership grants, and architecture tests.
