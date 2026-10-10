# ADR-012 — Authorization Scope Model

Status: Accepted

## Decision

Permissions are evaluated against tenant, legal-entity and branch scope using the authenticated membership subject. UI visibility is advisory; backend authorization is mandatory.

## Consequences

One identity may hold different scoped memberships without duplicate accounts, and cross-tenant or out-of-scope requests fail closed.

## Evidence

`backend/Core/Authorization`, API authorization endpoints, and authorization integration tests.
