SET row_security = off;

ALTER TABLE finance_banking.reconciliation_session
    ALTER COLUMN book_balance DROP NOT NULL,
    ALTER COLUMN difference DROP NOT NULL,
    ADD COLUMN IF NOT EXISTS ledger_source_snapshot jsonb,
    ADD COLUMN IF NOT EXISTS ledger_last_posted_at timestamptz,
    ADD COLUMN IF NOT EXISTS ledger_posted_line_count bigint NOT NULL DEFAULT 0;

ALTER TABLE finance_banking.cash_position_projection
    ALTER COLUMN book_balance DROP NOT NULL,
    ALTER COLUMN book_balance DROP DEFAULT;

CREATE TABLE IF NOT EXISTS finance_banking.reconciliation_system_claim (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    system_reference text NOT NULL,
    claimed_amount numeric(30,12) NOT NULL DEFAULT 0,
    version bigint NOT NULL DEFAULT 1,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,legal_entity_id,system_reference),
    FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (claimed_amount >= 0)
);
CREATE INDEX IF NOT EXISTS ix_banking_reconciliation_system_claim_scope
    ON finance_banking.reconciliation_system_claim(tenant_id,legal_entity_id,system_reference);
ALTER TABLE finance_banking.reconciliation_system_claim ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_banking.reconciliation_system_claim FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_banking.reconciliation_system_claim;
CREATE POLICY tenant_isolation ON finance_banking.reconciliation_system_claim
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE, DELETE ON finance_banking.reconciliation_system_claim TO erp_app;

RESET row_security;
RESET search_path;
