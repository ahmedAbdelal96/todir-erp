# ADR-018 — Import Framework

Status: Accepted

## Decision

Imports use explicit upload, detection, mapping, validation, preview, dry-run, execute, reconcile and terminal states. Large work is chunked, scheduled transactionally and resumed by tenant/batch/chunk idempotency keys.

## Consequences

Progress and retry behavior remain bounded and observable; rollback is never promised for already-posted legal data.

## Evidence

`backend/Core/Services/ImportSchedulingServices.cs`, import schema, and import scheduling tests.
