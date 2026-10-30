SET search_path = business_accounting, public;
SET row_security = off;

CREATE TABLE business_accounting.fx_revaluation_run (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    as_of_date date NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    status text NOT NULL,
    requested_by uuid NOT NULL,
    auto_reversal_date date,
    posted_at timestamptz,
    reversed_at timestamptz,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    row_version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, as_of_date, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (status IN ('Previewed','Posting','Posted','Reversed')),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
    CHECK (auto_reversal_date IS NULL OR auto_reversal_date > as_of_date),
    CHECK (row_version > 0)
);

CREATE TABLE business_accounting.fx_revaluation_line (
    tenant_id uuid NOT NULL,
    revaluation_run_id uuid NOT NULL,
    id uuid NOT NULL,
    open_item_id uuid NOT NULL,
    source_row_version bigint NOT NULL,
    direction text NOT NULL,
    transaction_currency char(3) NOT NULL,
    open_transaction_amount numeric(20,6) NOT NULL,
    base_currency char(3) NOT NULL,
    carrying_base_amount numeric(20,6) NOT NULL,
    revalued_base_amount numeric(20,6) NOT NULL,
    difference_amount numeric(20,6) NOT NULL,
    difference_role text NOT NULL,
    exchange_rate numeric(20,10) NOT NULL,
    exchange_rate_snapshot jsonb NOT NULL,
    ledger_fact_id uuid,
    journal_entry_id uuid,
    reversal_entry_id uuid,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, revaluation_run_id, id),
    UNIQUE (tenant_id, revaluation_run_id, open_item_id),
    FOREIGN KEY (tenant_id, revaluation_run_id) REFERENCES business_accounting.fx_revaluation_run(tenant_id, id),
    CHECK (direction IN ('Receivable','Payable')),
    CHECK (difference_role IN ('FXGain','FXLoss')),
    CHECK (transaction_currency ~ '^[A-Z]{3}$'),
    CHECK (base_currency ~ '^[A-Z]{3}$'),
    CHECK (open_transaction_amount > 0),
    CHECK (carrying_base_amount >= 0),
    CHECK (revalued_base_amount >= 0),
    CHECK (difference_amount <> 0),
    CHECK (exchange_rate > 0),
    CHECK (jsonb_typeof(exchange_rate_snapshot) = 'object')
);

CREATE UNIQUE INDEX ux_accounting_fx_revaluation_posted_date
    ON business_accounting.fx_revaluation_run(tenant_id, legal_entity_id, as_of_date)
    WHERE status IN ('Posting','Posted');
CREATE INDEX ix_accounting_fx_revaluation_run_scope
    ON business_accounting.fx_revaluation_run(tenant_id, legal_entity_id, as_of_date DESC, created_at DESC);

ALTER TABLE business_accounting.fx_revaluation_run ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_accounting.fx_revaluation_run FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_accounting.fx_revaluation_run
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
ALTER TABLE business_accounting.fx_revaluation_line ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_accounting.fx_revaluation_line FORCE ROW LEVEL SECURITY;
CREATE POLICY tenant_isolation ON business_accounting.fx_revaluation_line
    USING (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE ON business_accounting.fx_revaluation_run, business_accounting.fx_revaluation_line TO erp_app;
