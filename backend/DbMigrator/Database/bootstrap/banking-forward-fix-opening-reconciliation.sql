-- Module 25 opening/migration evidence. Opening balances are reconciled as a
-- semantic Banking record; no Payment or synthetic statement line is created.
CREATE TABLE IF NOT EXISTS finance_banking.opening_reconciliation (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    treasury_account_id uuid NOT NULL,
    ledger_opening_batch_id uuid NOT NULL,
    opening_as_of_date date NOT NULL,
    currency_code char(3) NOT NULL,
    book_opening_balance numeric(30,12) NOT NULL,
    external_opening_balance numeric(30,12) NOT NULL,
    outstanding_in_transit_amount numeric(30,12) NOT NULL DEFAULT 0,
    difference numeric(30,12) NOT NULL,
    source_system text NOT NULL,
    legacy_account_id text,
    batch_id text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL DEFAULT now(),
    finalized_by uuid,
    finalized_at timestamptz,
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,id),
    UNIQUE (tenant_id,legal_entity_id,treasury_account_id,ledger_opening_batch_id),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (currency_code ~ '^[A-Z]{3}$'),
    CHECK (outstanding_in_transit_amount >= 0),
    CHECK (difference = external_opening_balance - book_opening_balance - outstanding_in_transit_amount),
    CHECK (status IN ('Draft','Finalized')),
    CHECK (length(btrim(source_system)) BETWEEN 1 AND 200),
    CHECK (length(btrim(batch_id)) BETWEEN 1 AND 200),
    CHECK (version > 0)
);

CREATE INDEX IF NOT EXISTS ix_banking_opening_reconciliation_scope
    ON finance_banking.opening_reconciliation(tenant_id,legal_entity_id,treasury_account_id,opening_as_of_date DESC,id DESC);

ALTER TABLE finance_banking.opening_reconciliation ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_banking.opening_reconciliation FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON finance_banking.opening_reconciliation;
CREATE POLICY tenant_isolation ON finance_banking.opening_reconciliation
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
GRANT SELECT, INSERT, UPDATE ON finance_banking.opening_reconciliation TO erp_app;
