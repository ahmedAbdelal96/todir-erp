CREATE TABLE IF NOT EXISTS business_assets.asset_book_estimate_change (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_book_id uuid NOT NULL,
    effective_from date NOT NULL,
    useful_life_months integer,
    residual_value numeric(20,6),
    reason text NOT NULL,
    idempotency_key text NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,asset_book_id,effective_from),
    UNIQUE (tenant_id,idempotency_key),
    FOREIGN KEY (tenant_id,asset_book_id) REFERENCES business_assets.asset_book(tenant_id,id),
    CHECK (useful_life_months IS NOT NULL OR residual_value IS NOT NULL),
    CHECK (useful_life_months IS NULL OR useful_life_months > 0),
    CHECK (residual_value IS NULL OR residual_value >= 0),
    CHECK (length(btrim(reason)) BETWEEN 1 AND 500),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200)
);

ALTER TABLE business_assets.asset_book_estimate_change ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_book_estimate_change FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_book_estimate_change;
CREATE POLICY tenant_isolation ON business_assets.asset_book_estimate_change
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
CREATE INDEX IF NOT EXISTS ix_assets_estimate_changes_book_date
    ON business_assets.asset_book_estimate_change(tenant_id,asset_book_id,effective_from,id);
GRANT SELECT, INSERT, UPDATE ON business_assets.asset_book_estimate_change TO erp_app;
