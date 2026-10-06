# ADR-008 — Module Manifest and Registry

Status: Accepted

## Decision

Every module declares a versioned JSON manifest containing ownership, layer, hard dependencies, capabilities, permissions and lifecycle policy. The registry is built from validated manifests and migrations follow the hard-dependency topological order.

## Consequences

Unknown modules, duplicate identifiers and dependency cycles fail validation before deployment.

## Evidence

`src/backend/platform/module.manifest.json`, `scripts/architecture/validate-module-manifests.mjs`, and `Todir.DbMigrator`.
