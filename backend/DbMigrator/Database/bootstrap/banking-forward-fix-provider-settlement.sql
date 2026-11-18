SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_banking.provider_settlement_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, settlement_account_id uuid NOT NULL,
    provider_code text NOT NULL, provider_reference text NOT NULL, period_from date NOT NULL, period_to date NOT NULL,
    currency_code char(3) NOT NULL, gross_amount numeric(30,12) NOT NULL, fees_amount numeric(30,12) NOT NULL,
    adjustments_amount numeric(30,12) NOT NULL, net_payout_amount numeric(30,12) NOT NULL, external_reference text NOT NULL,
    source_message_id uuid NOT NULL, status text NOT NULL DEFAULT 'Imported', created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,provider_reference), UNIQUE (tenant_id,source_message_id),
    FOREIGN KEY (tenant_id,legal_entity_id,settlement_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (period_from <= period_to), CHECK (currency_code ~ '^[A-Z]{3}$'), CHECK (gross_amount >= 0), CHECK (fees_amount >= 0), CHECK (net_payout_amount >= 0),
    CHECK (gross_amount - fees_amount + adjustments_amount = net_payout_amount), CHECK (status IN ('Imported','Matched','Exception'))
);

CREATE INDEX IF NOT EXISTS ix_banking_provider_settlement_scope ON finance_banking.provider_settlement_projection(tenant_id,legal_entity_id,settlement_account_id,period_to DESC,id);
ALTER TABLE finance_banking.provider_settlement_projection ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_banking.provider_settlement_projection FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_banking.provider_settlement_projection;
CREATE POLICY tenant_isolation ON finance_banking.provider_settlement_projection USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON finance_banking.provider_settlement_projection TO erp_app;

RESET row_security;
RESET search_path;
