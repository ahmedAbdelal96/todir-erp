-- Sample module verification slice. It is disposable after the first real module is ready.
CREATE SCHEMA IF NOT EXISTS sample AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA sample FROM PUBLIC;
GRANT USAGE ON SCHEMA sample TO erp_app;

CREATE TABLE IF NOT EXISTS sample.sample_item
(
    id uuid PRIMARY KEY,
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    name text NOT NULL CHECK (length(name) BETWEEN 1 AND 200),
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    UNIQUE (tenant_id, id)
);

CREATE TABLE IF NOT EXISTS sample.sample_projection
(
    tenant_id uuid NOT NULL REFERENCES platform_tenancy.tenant (id),
    item_id uuid NOT NULL,
    projected_at timestamptz NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, item_id)
);

CREATE INDEX IF NOT EXISTS ix_sample_item_tenant_created ON sample.sample_item (tenant_id, created_at DESC, id);
CREATE INDEX IF NOT EXISTS ix_sample_projection_tenant_projected ON sample.sample_projection (tenant_id, projected_at DESC, item_id);
GRANT SELECT, INSERT, UPDATE, DELETE ON sample.sample_item, sample.sample_projection TO erp_app;

ALTER TABLE sample.sample_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE sample.sample_item FORCE ROW LEVEL SECURITY;
ALTER TABLE sample.sample_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE sample.sample_projection FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON sample.sample_item;
DROP POLICY IF EXISTS tenant_isolation ON sample.sample_projection;
CREATE POLICY tenant_isolation ON sample.sample_item USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_isolation ON sample.sample_projection USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
