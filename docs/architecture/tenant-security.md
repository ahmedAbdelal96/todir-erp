# Tenant security boundary

Tenant-owned tables use a non-null tenant key and PostgreSQL row-level security.
Policies compare that key with the transaction-local `app.tenant_id` setting.

Application code establishes the setting with `SET LOCAL` inside an explicit
transaction. Missing context is default-deny; a pooled connection must never
retain tenant state after the transaction ends.

The integration tests connect as the real runtime role and verify that one
tenant cannot read, update, or insert rows for another tenant. The migration
owner creates and owns the table; the runtime role has DML only. Tenant context
resolution from authentication and worker envelopes remains an application
platform concern and must not accept a tenant ID from an ordinary request body.
