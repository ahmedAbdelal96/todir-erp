CREATE TABLE IF NOT EXISTS business_assets.asset_impairment (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    asset_book_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    amount numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    reason text NOT NULL,
    approval_request_id uuid NOT NULL,
    evidence_file_ref uuid,
    status text NOT NULL DEFAULT 'Posted',
    idempotency_key text NOT NULL,
    ledger_fact_reference text,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,asset_book_id,idempotency_key),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_book_id) REFERENCES business_assets.asset_book(tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (amount > 0), CHECK (length(btrim(reason)) BETWEEN 1 AND 500),
    CHECK (status IN ('Posted','Reversed'))
);

CREATE TABLE IF NOT EXISTS business_assets.asset_revaluation (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    asset_book_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    proposed_gross_cost numeric(20,6) NOT NULL,
    proposed_residual_value numeric(20,6) NOT NULL,
    effective_date date NOT NULL,
    reason text NOT NULL,
    approval_request_id uuid NOT NULL,
    provider_code text NOT NULL,
    provider_version text NOT NULL,
    status text NOT NULL DEFAULT 'PendingProvider',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    FOREIGN KEY (tenant_id,asset_id) REFERENCES business_assets.fixed_asset(tenant_id,id),
    FOREIGN KEY (tenant_id,asset_book_id) REFERENCES business_assets.asset_book(tenant_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (proposed_gross_cost >= 0 AND proposed_residual_value >= 0 AND proposed_residual_value <= proposed_gross_cost),
    CHECK (length(btrim(reason)) BETWEEN 1 AND 500),
    CHECK (length(btrim(provider_code)) BETWEEN 1 AND 80), CHECK (length(btrim(provider_version)) BETWEEN 1 AND 80),
    CHECK (status IN ('PendingProvider','Validated','Rejected'))
);

ALTER TABLE business_assets.asset_impairment ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_impairment FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_impairment;
CREATE POLICY tenant_isolation ON business_assets.asset_impairment
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE business_assets.asset_revaluation ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_revaluation FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_assets.asset_revaluation;
CREATE POLICY tenant_isolation ON business_assets.asset_revaluation
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

CREATE INDEX IF NOT EXISTS ix_assets_impairment_book_date ON business_assets.asset_impairment(tenant_id,asset_book_id,effective_date,id);
CREATE INDEX IF NOT EXISTS ix_assets_revaluation_book_date ON business_assets.asset_revaluation(tenant_id,asset_book_id,effective_date,id);
GRANT SELECT, INSERT ON business_assets.asset_impairment TO erp_app;
GRANT SELECT, INSERT ON business_assets.asset_revaluation TO erp_app;
REVOKE UPDATE, DELETE ON business_assets.asset_impairment, business_assets.asset_revaluation FROM erp_app;
REVOKE UPDATE ON business_assets.asset_book_estimate_change FROM erp_app;
