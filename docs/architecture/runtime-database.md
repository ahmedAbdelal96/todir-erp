# Runtime database boundary

P04 establishes the PostgreSQL ownership and runtime permission boundary.

## Roles

- `erp_owner` is `NOLOGIN` and owns schemas/tables created by deployment migrations.
- `erp_app` is the API/Worker runtime role. It is not a superuser, cannot create
  databases or roles, has no `BYPASSRLS`, and does not own tenant data.
- Runtime credentials are provisioned by the deployment secret manager. No
  passwords are stored in the repository or the bootstrap SQL.

## Schemas and grants

The bootstrap creates the approved platform schema prefixes, removes `PUBLIC`
schema access, grants `erp_app` schema `USAGE`, and configures owner default
privileges for runtime DML. Each module migration remains responsible for
explicit grants on its existing tables, including stricter audit-table grants.

The API and Worker never apply production migrations at startup. The dedicated
migration host runs deployment migrations with `erp_owner`; runtime processes
connect only as `erp_app`.

## Boundary verification

The database security integration test creates a throwaway owner-created table,
connects through a real `erp_app` connection, verifies permitted DML, and proves
that the runtime role cannot alter the table. The test uses an ephemeral
PostgreSQL service and is not a production schema or tenant-data migration.

P04 does not implement tenant session context or row-level security. Those are
owned by P05.
