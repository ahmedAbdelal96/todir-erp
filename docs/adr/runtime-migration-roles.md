# ADR-003 — Runtime and Migration Database Roles

Status: Accepted

## Decision

`erp_owner` owns schemas and database objects, `erp_app` is the least-privileged runtime login, and `erp_dispatcher` is a non-login owner only for fixed-parameter security-definer dispatcher functions. Only `Todir.DbMigrator` receives migration credentials.

## Consequences

API and Worker startup cannot silently change schema. Grants and ownership must be reasserted by controlled migrations.

## Evidence

`ops/database/bootstrap/runtime-roles-and-schemas.sql`, `src/backend/hosts/database-migrator/Program.cs`, and role-boundary tests.
