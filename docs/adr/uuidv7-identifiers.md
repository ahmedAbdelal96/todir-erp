# ADR-013 — UUIDv7 Identifiers

Status: Accepted

## Decision

New durable identifiers use UUIDv7 through the shared identifier abstractions. IDs are opaque at API boundaries and are not used as authorization evidence by themselves.

## Consequences

Identifiers retain time locality for indexes while remaining globally unique and independent of business numbering.

## Evidence

`backend/Shared/Primitives/Identifiers` and shared primitive tests.
