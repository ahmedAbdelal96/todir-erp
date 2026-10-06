-- Tenant security boundary bootstrap. Execute as the migration owner.

CREATE TABLE IF NOT EXISTS platform_tenancy.tenant
(
    id uuid PRIMARY KEY,
    slug text NOT NULL UNIQUE,
    display_name text NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL
);

ALTER TABLE platform_tenancy.tenant ENABLE ROW LEVEL SECURITY;
ALTER TABLE platform_tenancy.tenant FORCE ROW LEVEL SECURITY;

DROP POLICY IF EXISTS tenant_isolation ON platform_tenancy.tenant;
CREATE POLICY tenant_isolation ON platform_tenancy.tenant
    USING (id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
