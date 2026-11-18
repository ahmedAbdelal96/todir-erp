SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_banking.pos_cash_settlement_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, pos_session_id uuid NOT NULL,
    register_id uuid NOT NULL, branch_id uuid NOT NULL, business_date date NOT NULL, currency_code char(3) NOT NULL,
    expected_cash numeric(30,12) NOT NULL, counted_cash numeric(30,12) NOT NULL, variance numeric(30,12) NOT NULL,
    settlement_status text NOT NULL DEFAULT 'Pending', treasury_account_id uuid, source_message_id uuid NOT NULL,
    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,pos_session_id), UNIQUE (tenant_id,source_message_id),
    CHECK (currency_code ~ '^[A-Z]{3}$'), CHECK (expected_cash >= 0), CHECK (counted_cash >= 0),
    CHECK (settlement_status IN ('Pending','Confirmed','Rejected')),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id)
);

CREATE INDEX IF NOT EXISTS ix_banking_pos_settlement_scope ON finance_banking.pos_cash_settlement_projection(tenant_id,legal_entity_id,settlement_status,business_date,id);

ALTER TABLE finance_banking.pos_cash_settlement_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_banking.pos_cash_settlement_projection FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_banking.pos_cash_settlement_projection;
CREATE POLICY tenant_isolation ON finance_banking.pos_cash_settlement_projection USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON finance_banking.pos_cash_settlement_projection TO erp_app;

RESET row_security;
RESET search_path;
