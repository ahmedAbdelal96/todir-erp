# Row-level security

Every tenant-owned table has a non-null tenant key, a tenant isolation policy, and `FORCE ROW LEVEL SECURITY`. `erp_app` is not a table owner and has no `BYPASSRLS`. Cross-tenant relationships use composite foreign keys where the parent is tenant-owned. The database security suite is the release gate for these invariants.
Tenant-owned tables use PostgreSQL FORCE RLS with a transaction-local `app.tenant_id`. Runtime application connections use `erp_app`, which is explicitly non-superuser and does not bypass RLS. The only cross-tenant background operation currently approved is the fixed-parameter outbox claim function, owned by the non-login `erp_dispatcher` role and limited to pending message selection.
