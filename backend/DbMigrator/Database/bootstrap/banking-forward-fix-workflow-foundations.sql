SET row_security = off;

CREATE TABLE IF NOT EXISTS finance_banking.external_feed_transaction (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL,
    source_type text NOT NULL, external_transaction_id text NOT NULL, content_hash char(64) NOT NULL, statement_line_id uuid,
    booking_date date NOT NULL, direction text NOT NULL, amount numeric(30,12) NOT NULL, currency_code char(3) NOT NULL,
    status text NOT NULL DEFAULT 'Received', idempotency_key text NOT NULL, raw_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,treasury_account_id,source_type,external_transaction_id), UNIQUE (tenant_id,treasury_account_id,content_hash),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id,statement_line_id) REFERENCES finance_banking.external_statement_line(tenant_id,legal_entity_id,id),
    CHECK (direction IN ('Credit','Debit')), CHECK (amount > 0), CHECK (currency_code ~ '^[A-Z]{3}$'),
    CHECK (status IN ('Received','Validated','Projected','Rejected','Superseded')), CHECK (jsonb_typeof(raw_payload) = 'object')
);

CREATE TABLE IF NOT EXISTS finance_banking.bank_originated_event_candidate (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, statement_line_id uuid NOT NULL,
    event_type text NOT NULL, status text NOT NULL DEFAULT 'Proposed', amount numeric(30,12) NOT NULL, currency_code char(3) NOT NULL,
    evidence jsonb NOT NULL DEFAULT '{}'::jsonb, proposed_at timestamptz NOT NULL, reviewed_by uuid, reviewed_at timestamptz,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,statement_line_id,event_type),
    FOREIGN KEY (tenant_id,legal_entity_id,statement_line_id) REFERENCES finance_banking.external_statement_line(tenant_id,legal_entity_id,id),
    CHECK (status IN ('Proposed','Approved','Rejected','Superseded')), CHECK (amount > 0), CHECK (currency_code ~ '^[A-Z]{3}$'), CHECK (jsonb_typeof(evidence) = 'object')
);

CREATE TABLE IF NOT EXISTS finance_banking.treasury_activity_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL,
    activity_type text NOT NULL, source_reference text NOT NULL, business_date date NOT NULL, direction text NOT NULL,
    amount numeric(30,12) NOT NULL, currency_code char(3) NOT NULL, status text NOT NULL, source_watermark timestamptz,
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,treasury_account_id,source_reference),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (direction IN ('Credit','Debit')), CHECK (amount > 0), CHECK (currency_code ~ '^[A-Z]{3}$')
);

CREATE TABLE IF NOT EXISTS finance_banking.treasury_account_balance_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, currency_code char(3) NOT NULL,
    book_balance numeric(30,12) NOT NULL DEFAULT 0, external_balance numeric(30,12), available_balance numeric(30,12),
    unreconciled_amount numeric(30,12) NOT NULL DEFAULT 0, last_reconciled_at timestamptz, source_watermark timestamptz,
    as_of timestamptz NOT NULL, PRIMARY KEY (tenant_id,treasury_account_id,currency_code),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (currency_code ~ '^[A-Z]{3}$')
);

CREATE TABLE IF NOT EXISTS finance_banking.transfer_reconciliation_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, source_account_id uuid NOT NULL, destination_account_id uuid NOT NULL,
    transfer_reference text NOT NULL, state text NOT NULL, source_evidence text, destination_evidence text, source_amount numeric(30,12),
    destination_amount numeric(30,12), currency_code char(3), in_transit_amount numeric(30,12) NOT NULL DEFAULT 0, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,transfer_reference),
    FOREIGN KEY (tenant_id,legal_entity_id,source_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id,destination_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (state IN ('Initiated','SourceConfirmed','DestinationConfirmed','Matched','InTransit','Exception')), CHECK (currency_code IS NULL OR currency_code ~ '^[A-Z]{3}$'), CHECK (source_account_id <> destination_account_id)
);

CREATE TABLE IF NOT EXISTS finance_banking.settlement_account_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, currency_code char(3) NOT NULL,
    gross_pending numeric(30,12) NOT NULL DEFAULT 0, fees_pending numeric(30,12) NOT NULL DEFAULT 0, net_expected numeric(30,12) NOT NULL DEFAULT 0,
    as_of timestamptz NOT NULL, PRIMARY KEY (tenant_id,treasury_account_id,currency_code),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (currency_code ~ '^[A-Z]{3}$')
);

CREATE TABLE IF NOT EXISTS finance_banking.cash_forecast_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, forecast_version_id uuid NOT NULL, treasury_account_id uuid,
    currency_code char(3) NOT NULL, projected_amount numeric(30,12) NOT NULL, as_of timestamptz NOT NULL, source text NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,forecast_version_id,treasury_account_id,currency_code),
    FOREIGN KEY (tenant_id,legal_entity_id,forecast_version_id) REFERENCES finance_banking.cash_forecast_version(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (currency_code ~ '^[A-Z]{3}$')
);

CREATE INDEX IF NOT EXISTS ix_banking_feed_scope ON finance_banking.external_feed_transaction(tenant_id,legal_entity_id,treasury_account_id,booking_date,id);
CREATE INDEX IF NOT EXISTS ix_banking_event_candidate_scope ON finance_banking.bank_originated_event_candidate(tenant_id,legal_entity_id,status,proposed_at,id);
CREATE INDEX IF NOT EXISTS ix_banking_activity_scope ON finance_banking.treasury_activity_projection(tenant_id,legal_entity_id,treasury_account_id,business_date,id);
CREATE INDEX IF NOT EXISTS ix_banking_transfer_scope ON finance_banking.transfer_reconciliation_projection(tenant_id,legal_entity_id,state,updated_at,id);

DO $$ DECLARE item text; BEGIN
    FOREACH item IN ARRAY ARRAY['external_feed_transaction','bank_originated_event_candidate','treasury_activity_projection','treasury_account_balance_projection','transfer_reconciliation_projection','settlement_account_projection','cash_forecast_projection'] LOOP
        EXECUTE format('ALTER TABLE finance_banking.%I ENABLE ROW LEVEL SECURITY', item);
        EXECUTE format('ALTER TABLE finance_banking.%I FORCE ROW LEVEL SECURITY', item);
        IF NOT EXISTS (SELECT 1 FROM pg_policies WHERE schemaname='finance_banking' AND tablename=item AND policyname='tenant_isolation') THEN
            EXECUTE format('CREATE POLICY tenant_isolation ON finance_banking.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
        END IF;
        EXECUTE format('GRANT SELECT, INSERT, UPDATE ON finance_banking.%I TO erp_app', item);
    END LOOP;
END $$;

RESET row_security;
RESET search_path;
