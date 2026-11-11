CREATE TABLE IF NOT EXISTS business_assets.asset_cip_split_allocation (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    cip_asset_id uuid NOT NULL,
    target_asset_id uuid NOT NULL,
    cost_source_id uuid NOT NULL,
    split_key text NOT NULL,
    amount numeric(20,6) NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,split_key,cost_source_id,target_asset_id),
    FOREIGN KEY (tenant_id,cip_asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,target_asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,cost_source_id) REFERENCES business_assets.asset_cip_cost_source(tenant_id,id),
    CHECK (amount > 0)
);

ALTER TABLE business_assets.asset_cip_split_allocation ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_cip_split_allocation FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_cip_split_allocation;
CREATE POLICY tenant_isolation ON business_assets.asset_cip_split_allocation
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_assets_cip_split_source
    ON business_assets.asset_cip_split_allocation(tenant_id,cip_asset_id,cost_source_id);

GRANT SELECT, INSERT ON business_assets.asset_cip_split_allocation TO erp_app;
