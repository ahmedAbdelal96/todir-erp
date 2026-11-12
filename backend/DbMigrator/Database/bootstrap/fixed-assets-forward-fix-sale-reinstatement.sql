ALTER TABLE business_assets.asset_book_transaction
    DROP CONSTRAINT IF EXISTS asset_book_transaction_type_check;

ALTER TABLE business_assets.asset_book_transaction
    ADD CONSTRAINT asset_book_transaction_type_check
    CHECK (transaction_type IN ('Opening','Acquisition','CapitalAddition','CostReduction','Depreciation','Impairment','Revaluation','Disposal','Reversal','Reinstatement'));

CREATE TABLE IF NOT EXISTS business_assets.asset_sale (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    disposal_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    invoice_id uuid NOT NULL,
    invoice_snapshot jsonb NOT NULL,
    payment_reference text,
    status text NOT NULL DEFAULT 'Active',
    reinstatement_id uuid,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, disposal_id),
    CONSTRAINT asset_sale_status_check CHECK (status IN ('Active','Reinstated')),
    CONSTRAINT asset_sale_asset_fk FOREIGN KEY (tenant_id, asset_id) REFERENCES business_assets.fixed_asset(tenant_id, id),
    CONSTRAINT asset_sale_disposal_fk FOREIGN KEY (tenant_id, disposal_id) REFERENCES business_assets.asset_disposal(tenant_id, id)
);

CREATE TABLE IF NOT EXISTS business_assets.asset_disposal_reinstatement (
    tenant_id uuid NOT NULL,
    id uuid NOT NULL,
    asset_id uuid NOT NULL,
    disposal_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    idempotency_key text NOT NULL,
    request_hash text NOT NULL,
    reinstatement_date date NOT NULL,
    reason text NOT NULL,
    status text NOT NULL DEFAULT 'InProgress',
    response jsonb,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL DEFAULT now(),
    completed_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, idempotency_key),
    CONSTRAINT asset_reinstatement_status_check CHECK (status IN ('InProgress','Completed')),
    CONSTRAINT asset_reinstatement_asset_fk FOREIGN KEY (tenant_id, asset_id) REFERENCES business_assets.fixed_asset(tenant_id, id),
    CONSTRAINT asset_reinstatement_disposal_fk FOREIGN KEY (tenant_id, disposal_id) REFERENCES business_assets.asset_disposal(tenant_id, id)
);

ALTER TABLE business_assets.asset_sale ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_sale FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS asset_sale_tenant_isolation ON business_assets.asset_sale;
CREATE POLICY asset_sale_tenant_isolation ON business_assets.asset_sale
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);

ALTER TABLE business_assets.asset_disposal_reinstatement ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_assets.asset_disposal_reinstatement FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS asset_disposal_reinstatement_tenant_isolation ON business_assets.asset_disposal_reinstatement;
CREATE POLICY asset_disposal_reinstatement_tenant_isolation ON business_assets.asset_disposal_reinstatement
    USING (tenant_id = current_setting('app.tenant_id', true)::uuid)
    WITH CHECK (tenant_id = current_setting('app.tenant_id', true)::uuid);

GRANT SELECT, INSERT, UPDATE ON business_assets.asset_sale TO erp_app;
GRANT SELECT, INSERT, UPDATE ON business_assets.asset_disposal_reinstatement TO erp_app;
CREATE INDEX IF NOT EXISTS ix_asset_sale_invoice ON business_assets.asset_sale(tenant_id, legal_entity_id, invoice_id);
CREATE INDEX IF NOT EXISTS ix_asset_reinstatement_disposal ON business_assets.asset_disposal_reinstatement(tenant_id, disposal_id);
