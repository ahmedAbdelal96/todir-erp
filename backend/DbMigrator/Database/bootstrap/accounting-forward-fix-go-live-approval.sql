SET search_path = business_accounting, public;
SET row_security = off;

CREATE TABLE IF NOT EXISTS business_accounting.go_live_approval (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    opening_batch_id uuid NOT NULL,
    subledger_opening_batch_id uuid,
    go_live_date date NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    reason text NOT NULL,
    checks jsonb NOT NULL DEFAULT '[]'::jsonb,
    approved_by uuid NOT NULL,
    approved_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
    CHECK (jsonb_typeof(checks) = 'array')
);

ALTER TABLE business_accounting.go_live_approval ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_accounting.go_live_approval FORCE ROW LEVEL SECURITY;
DO $$ BEGIN
    CREATE POLICY tenant_isolation ON business_accounting.go_live_approval
        USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
        WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
EXCEPTION WHEN duplicate_object THEN NULL;
END $$;
CREATE INDEX IF NOT EXISTS ix_accounting_go_live_scope ON business_accounting.go_live_approval(tenant_id, legal_entity_id, approved_at DESC);
GRANT SELECT, INSERT, UPDATE ON business_accounting.go_live_approval TO erp_app;
