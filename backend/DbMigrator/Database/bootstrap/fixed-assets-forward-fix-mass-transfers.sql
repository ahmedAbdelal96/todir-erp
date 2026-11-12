CREATE TABLE IF NOT EXISTS business_assets.asset_mass_transfer (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    idempotency_key text NOT NULL,
    request_hash char(64) NOT NULL,
    status text NOT NULL DEFAULT 'InProgress',
    requested_count integer NOT NULL,
    applied_count integer NOT NULL DEFAULT 0,
    response jsonb,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    completed_at timestamptz,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (request_hash ~ '^[0-9a-f]{64}$'),
    CHECK (status IN ('InProgress','Completed','Rejected')),
    CHECK (requested_count > 0 AND applied_count >= 0 AND applied_count <= requested_count)
);

CREATE TABLE IF NOT EXISTS business_assets.asset_mass_transfer_item (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    operation_id uuid NOT NULL,
    asset_id uuid NOT NULL,
    assignment_id uuid NOT NULL,
    status text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,operation_id,asset_id),
    FOREIGN KEY (tenant_id,operation_id) REFERENCES business_assets.asset_mass_transfer(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,assignment_id) REFERENCES business_assets.asset_assignment(tenant_id,id),
    CHECK (status IN ('Applied'))
);

ALTER TABLE business_assets.asset_mass_transfer ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_mass_transfer FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_mass_transfer;
CREATE POLICY tenant_isolation ON business_assets.asset_mass_transfer
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

ALTER TABLE business_assets.asset_mass_transfer_item ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_mass_transfer_item FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_mass_transfer_item;
CREATE POLICY tenant_isolation ON business_assets.asset_mass_transfer_item
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_assets_mass_transfer_items ON business_assets.asset_mass_transfer_item(tenant_id,operation_id,asset_id);
GRANT SELECT, INSERT, UPDATE ON business_assets.asset_mass_transfer TO erp_app;
GRANT SELECT, INSERT ON business_assets.asset_mass_transfer_item TO erp_app;
