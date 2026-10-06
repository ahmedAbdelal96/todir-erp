# ADR-002 — PostgreSQL RLS Multi-Tenancy

Status: Accepted

## Decision

Tenant-owned tables carry a non-null tenant key and use PostgreSQL `FORCE ROW LEVEL SECURITY`. Runtime connections use `erp_app` with a transaction-local `app.tenant_id`; cross-tenant work is limited to explicitly approved security-definer operations.

## Consequences

Application filters remain useful for query intent but are never the security boundary. Every tenant access path must establish context inside its transaction.

## Evidence

`ops/database/bootstrap/tenant-boundary.sql`, `ops/database/bootstrap/platform-kernel-schema.sql`, and the database security suite.
