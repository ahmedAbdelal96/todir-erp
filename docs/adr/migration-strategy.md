# ADR-020 — Production Migration Strategy

Status: Accepted

## Decision

`backend/DbMigrator/DbMigrator.csproj` is the sole schema-writer host. It discovers module manifests, applies foundational and module scripts in deterministic hard-dependency order, records checksums in migration history, rejects edits to applied scripts, and requires expand/contract for incompatible changes. API and Worker never migrate on startup.

## Consequences

Clean staging and production deployment do not require manual database edits. Forward fixes are additive and separately traceable.

## Evidence

`backend/DbMigrator/Program.cs`, `ops/database/bootstrap`, migration policy validation, and the CI migration step.
