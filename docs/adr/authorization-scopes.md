# ADR-012 — Authorization Scope Model

Status: Accepted

## Decision

Permissions are evaluated against tenant, legal-entity and branch scope using the authenticated tenant-bound authorization subject. UI visibility is advisory; backend authorization is mandatory.

## Consequences

Each account has one tenant-bound authorization subject; cross-tenant or out-of-scope requests fail closed. Separate accounts are required for separate tenants.

## Evidence

`backend/Core/Authorization`, API authorization endpoints, and authorization integration tests.
