CREATE TABLE business_assets.asset_source_allocation_correction (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    allocation_id uuid NOT NULL,
    candidate_id uuid NOT NULL,
    asset_id uuid NOT NULL,
    amount numeric(20,6) NOT NULL,
    reason text NOT NULL,
    idempotency_key text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,allocation_id) REFERENCES business_assets.asset_source_allocation(tenant_id,id),
    FOREIGN KEY (tenant_id,candidate_id) REFERENCES business_assets.asset_capitalization_candidate(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    CHECK (amount > 0),
    CHECK (length(btrim(reason)) BETWEEN 1 AND 500),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200)
);

ALTER TABLE business_assets.asset_source_allocation_correction ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_source_allocation_correction FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_assets.asset_source_allocation_correction
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE INDEX ix_assets_allocation_corrections_allocation
    ON business_assets.asset_source_allocation_correction(tenant_id,allocation_id,created_at,id);
GRANT SELECT, INSERT, UPDATE ON business_assets.asset_source_allocation_correction TO erp_app;
