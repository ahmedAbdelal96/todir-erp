SET search_path = finance_banking, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS finance_banking AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA finance_banking FROM PUBLIC;
GRANT USAGE ON SCHEMA finance_banking TO erp_app;

CREATE TABLE finance_banking.treasury_account (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, branch_id uuid,
    code text NOT NULL, name text NOT NULL, account_type text NOT NULL, status text NOT NULL DEFAULT 'Draft',
    currency_policy text NOT NULL DEFAULT 'SingleCurrency', currency_code char(3), purpose text NOT NULL,
    liquidity_class text NOT NULL, allowed_uses jsonb NOT NULL DEFAULT '[]'::jsonb, masked_identifier text,
    secret_ref text, version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,code),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id),
    CHECK (account_type IN ('Bank','Cash','PettyCash','DigitalWallet','ProviderSettlement')),
    CHECK (status IN ('Draft','Active','Suspended','Closing','Closed')),
    CHECK (currency_policy IN ('SingleCurrency','MultiCurrency')),
    CHECK ((currency_policy='SingleCurrency') = (currency_code IS NOT NULL)),
    CHECK (currency_code IS NULL OR currency_code ~ '^[A-Z]{3}$'), CHECK (jsonb_typeof(allowed_uses)='array'),
    CHECK (length(btrim(code)) BETWEEN 1 AND 80 AND length(btrim(name)) BETWEEN 1 AND 200), CHECK (version>0),
    CHECK (masked_identifier IS NULL OR length(masked_identifier) <= 100), CHECK (secret_ref IS NULL OR length(secret_ref) <= 500)
);

CREATE TABLE finance_banking.treasury_account_balance_pocket (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL,
    currency_code char(3) NOT NULL, is_primary boolean NOT NULL DEFAULT false, version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,treasury_account_id,currency_code), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (currency_code ~ '^[A-Z]{3}$'), CHECK (version>0)
);

CREATE TABLE finance_banking.bank_account_profile (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, bank_name text NOT NULL,
    country_code char(2), iban_masked text, account_number_masked text, account_holder text, connection_ref text,
    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,treasury_account_id),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (length(btrim(bank_name)) BETWEEN 1 AND 200)
);

CREATE TABLE finance_banking.cash_account_profile (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, location_description text,
    custodian_required boolean NOT NULL DEFAULT true, denomination_policy text NOT NULL DEFAULT 'Configured', max_cash_amount numeric(20,6), minimum_float_amount numeric(20,6),
    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,treasury_account_id),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (max_cash_amount IS NULL OR max_cash_amount>=0), CHECK (minimum_float_amount IS NULL OR minimum_float_amount>=0)
);

CREATE TABLE finance_banking.digital_wallet_profile (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, provider_name text NOT NULL,
    wallet_reference_masked text, merchant_reference text, connection_ref text, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,treasury_account_id), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id)
);

CREATE TABLE finance_banking.settlement_account_profile (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, provider_code text NOT NULL,
    merchant_reference_masked text, settlement_delay_days integer NOT NULL DEFAULT 0, connection_ref text, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,treasury_account_id), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (settlement_delay_days>=0)
);

CREATE TABLE finance_banking.treasury_account_change_request (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, requested_by uuid NOT NULL,
    status text NOT NULL DEFAULT 'Pending', request_payload jsonb NOT NULL, reason text NOT NULL, approved_by uuid, version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL, decided_at timestamptz, PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    CHECK (status IN ('Pending','Approved','Rejected','Cancelled')), CHECK (jsonb_typeof(request_payload)='object'), CHECK (length(btrim(reason))>0)
);

CREATE TABLE finance_banking.treasury_account_connection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, provider_code text NOT NULL,
    connection_ref text NOT NULL, status text NOT NULL DEFAULT 'Disconnected', last_success_at timestamptz, last_failure_at timestamptz, failure_code text,
    version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id),
    UNIQUE (tenant_id,treasury_account_id,provider_code), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (status IN ('Connected','Disconnected','Degraded','Suspended'))
);

CREATE TABLE finance_banking.statement_import_batch (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, external_statement_id uuid,
    idempotency_key text NOT NULL, source_type text NOT NULL, status text NOT NULL, line_count integer NOT NULL DEFAULT 0, error_count integer NOT NULL DEFAULT 0,
    created_at timestamptz NOT NULL, completed_at timestamptz, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,treasury_account_id,idempotency_key),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (status IN ('Received','Validating','Completed','Failed','Superseded')), CHECK (line_count>=0)
);

CREATE TABLE finance_banking.external_statement (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, statement_number text NOT NULL,
    opening_date date, closing_date date, currency_code char(3) NOT NULL, opening_balance numeric(30,12), closing_balance numeric(30,12),
    processing_state text NOT NULL DEFAULT 'Imported', reconciliation_state text NOT NULL DEFAULT 'Unreconciled', source_type text NOT NULL, source_reference text,
    supersedes_statement_id uuid, version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,treasury_account_id,statement_number), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,supersedes_statement_id) REFERENCES finance_banking.external_statement(tenant_id,id), CHECK (currency_code ~ '^[A-Z]{3}$'), CHECK (processing_state IN ('Imported','Validated','Rejected','Superseded')), CHECK (reconciliation_state IN ('Unreconciled','InProgress','Reconciled'))
);
ALTER TABLE finance_banking.statement_import_batch ADD CONSTRAINT fk_banking_import_statement FOREIGN KEY (tenant_id,external_statement_id) REFERENCES finance_banking.external_statement(tenant_id,id);

CREATE TABLE finance_banking.external_statement_line (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, statement_id uuid NOT NULL, treasury_account_id uuid NOT NULL,
    external_line_id text NOT NULL, booking_date date NOT NULL, value_date date, direction text NOT NULL, amount numeric(30,12) NOT NULL,
    currency_code char(3) NOT NULL, reference text, transaction_code text, counterparty_name text, raw_payload jsonb NOT NULL DEFAULT '{}'::jsonb,
    content_hash char(64) NOT NULL, classification text NOT NULL DEFAULT 'Unclassified', claimed_amount numeric(30,12) NOT NULL DEFAULT 0,
    version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id),
    UNIQUE (tenant_id,treasury_account_id,external_line_id), FOREIGN KEY (tenant_id,legal_entity_id,statement_id) REFERENCES finance_banking.external_statement(tenant_id,legal_entity_id,id),
    FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (direction IN ('Credit','Debit')), CHECK (amount>0 AND claimed_amount>=0 AND claimed_amount<=amount), CHECK (classification IN ('Unclassified','Receipt','Payment','BankFee','Interest','Transfer','DirectDebit','ReturnedCheque','Other'))
);

CREATE TABLE finance_banking.statement_transaction_code_mapping (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, transaction_code text NOT NULL, classification text NOT NULL,
    effective_from date NOT NULL, effective_to date, version integer NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, created_by uuid NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,transaction_code,effective_from), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id), CHECK (effective_to IS NULL OR effective_to>=effective_from)
);

CREATE TABLE finance_banking.reconciliation_rule_set (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, name text NOT NULL, status text NOT NULL DEFAULT 'Draft', version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,name), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id), CHECK (status IN ('Draft','Active','Retired'))
);
CREATE TABLE finance_banking.reconciliation_rule_version (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, rule_set_id uuid NOT NULL, rule_version integer NOT NULL, priority integer NOT NULL, definition jsonb NOT NULL,
    created_at timestamptz NOT NULL, created_by uuid NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,rule_set_id,rule_version), FOREIGN KEY (tenant_id,legal_entity_id,rule_set_id) REFERENCES finance_banking.reconciliation_rule_set(tenant_id,legal_entity_id,id), CHECK (jsonb_typeof(definition)='object')
);

CREATE TABLE finance_banking.reconciliation_session (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, statement_id uuid NOT NULL,
    as_of_date date NOT NULL, state text NOT NULL DEFAULT 'Open', external_balance numeric(30,12) NOT NULL, book_balance numeric(30,12) NOT NULL, difference numeric(30,12) NOT NULL,
    notes text, finalized_by uuid, finalized_at timestamptz, version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,statement_id), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), FOREIGN KEY (tenant_id,legal_entity_id,statement_id) REFERENCES finance_banking.external_statement(tenant_id,legal_entity_id,id), CHECK (state IN ('Open','Finalized','Reopened'))
);
CREATE TABLE finance_banking.reconciliation_match (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, session_id uuid NOT NULL, source text NOT NULL, state text NOT NULL DEFAULT 'Draft', external_amount numeric(30,12) NOT NULL, system_amount numeric(30,12) NOT NULL, difference numeric(30,12) NOT NULL, explanation text,
    created_by uuid NOT NULL, finalized_at timestamptz, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), FOREIGN KEY (tenant_id,legal_entity_id,session_id) REFERENCES finance_banking.reconciliation_session(tenant_id,legal_entity_id,id), CHECK (state IN ('Draft','Accepted','Rejected','Removed')), CHECK (external_amount>=0 AND system_amount>=0), CHECK (external_amount=system_amount+difference)
);
CREATE TABLE finance_banking.reconciliation_match_item (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, match_id uuid NOT NULL, statement_line_id uuid, external_amount numeric(30,12) NOT NULL, system_reference text,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,match_id) REFERENCES finance_banking.reconciliation_match(tenant_id,legal_entity_id,id), FOREIGN KEY (tenant_id,legal_entity_id,statement_line_id) REFERENCES finance_banking.external_statement_line(tenant_id,legal_entity_id,id), CHECK (external_amount>0)
);
CREATE TABLE finance_banking.reconciliation_adjustment (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, session_id uuid NOT NULL, adjustment_type text NOT NULL, amount numeric(30,12) NOT NULL, currency_code char(3) NOT NULL, explanation text NOT NULL, status text NOT NULL DEFAULT 'Pending', created_by uuid NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,session_id) REFERENCES finance_banking.reconciliation_session(tenant_id,legal_entity_id,id), CHECK (amount<>0), CHECK (status IN ('Pending','Approved','Rejected'))
);

CREATE TABLE finance_banking.cash_count (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, cutoff_date date NOT NULL, custodian_id uuid NOT NULL, state text NOT NULL DEFAULT 'Draft', expected_amount numeric(20,6) NOT NULL, counted_amount numeric(20,6), difference numeric(20,6), version bigint NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, submitted_at timestamptz, finalized_at timestamptz,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,treasury_account_id,cutoff_date), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (state IN ('Draft','Submitted','Approved','Finalized','Rejected')), CHECK (expected_amount>=0)
);
CREATE TABLE finance_banking.cash_count_denomination (
    tenant_id uuid NOT NULL, id uuid NOT NULL, cash_count_id uuid NOT NULL, denomination text NOT NULL, unit_value numeric(20,6) NOT NULL, quantity integer NOT NULL, amount numeric(20,6) GENERATED ALWAYS AS (unit_value*quantity) STORED, PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,cash_count_id) REFERENCES finance_banking.cash_count(tenant_id,id), CHECK (unit_value>0 AND quantity>=0)
);
CREATE TABLE finance_banking.cash_difference_resolution (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, cash_count_id uuid NOT NULL, reason_code text NOT NULL, resolution_type text NOT NULL, amount numeric(20,6) NOT NULL, evidence_reference text, approved_by uuid, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,cash_count_id) REFERENCES finance_banking.cash_count(tenant_id,legal_entity_id,id), CHECK (amount<>0)
);
CREATE TABLE finance_banking.cash_custodian_history (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, treasury_account_id uuid NOT NULL, from_custodian_id uuid, to_custodian_id uuid NOT NULL, effective_at timestamptz NOT NULL, reason text NOT NULL, created_by uuid NOT NULL, PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id)
);

CREATE TABLE finance_banking.cash_position_projection (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, treasury_account_id uuid NOT NULL, currency_code char(3) NOT NULL, book_balance numeric(30,12) NOT NULL DEFAULT 0, external_balance numeric(30,12), available_balance numeric(30,12), in_transit_amount numeric(30,12) NOT NULL DEFAULT 0, balance_basis text NOT NULL, source_watermark timestamptz, as_of timestamptz NOT NULL, external_as_of timestamptz, PRIMARY KEY (tenant_id,treasury_account_id,currency_code), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id)
);
CREATE TABLE finance_banking.cash_forecast_version (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, as_of_date date NOT NULL, horizon_days integer NOT NULL, status text NOT NULL DEFAULT 'Draft', created_by uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,id), UNIQUE (tenant_id,legal_entity_id,as_of_date,horizon_days), FOREIGN KEY (tenant_id,legal_entity_id) REFERENCES platform.legal_entity(tenant_id,id), CHECK (horizon_days BETWEEN 1 AND 366), CHECK (status IN ('Draft','Published','Superseded'))
);
CREATE TABLE finance_banking.cash_forecast_item (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, forecast_version_id uuid NOT NULL, treasury_account_id uuid, due_date date NOT NULL, direction text NOT NULL, amount numeric(30,12) NOT NULL, currency_code char(3) NOT NULL, source_type text NOT NULL, source_reference jsonb, description text NOT NULL, confidence text NOT NULL DEFAULT 'Estimated', PRIMARY KEY (tenant_id,id), FOREIGN KEY (tenant_id,legal_entity_id,forecast_version_id) REFERENCES finance_banking.cash_forecast_version(tenant_id,legal_entity_id,id), FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id), CHECK (direction IN ('Inflow','Outflow')), CHECK (amount>0), CHECK (confidence IN ('Actual','Expected','Estimated','Unknown'))
);

CREATE INDEX ix_banking_account_scope ON finance_banking.treasury_account(tenant_id,legal_entity_id,status,code,id);
CREATE INDEX ix_banking_statement_scope ON finance_banking.external_statement(tenant_id,legal_entity_id,treasury_account_id,closing_date DESC,id DESC);
CREATE INDEX ix_banking_line_scope ON finance_banking.external_statement_line(tenant_id,legal_entity_id,statement_id,booking_date,id);
CREATE INDEX ix_banking_line_unclaimed ON finance_banking.external_statement_line(tenant_id,treasury_account_id,booking_date,id) WHERE claimed_amount < amount;
CREATE INDEX ix_banking_reconciliation_scope ON finance_banking.reconciliation_session(tenant_id,legal_entity_id,treasury_account_id,as_of_date DESC,id DESC);
CREATE INDEX ix_banking_cash_count_scope ON finance_banking.cash_count(tenant_id,legal_entity_id,treasury_account_id,cutoff_date DESC,id DESC);
CREATE INDEX ix_banking_forecast_scope ON finance_banking.cash_forecast_version(tenant_id,legal_entity_id,as_of_date DESC,id DESC);

DO $$ DECLARE item text; BEGIN
    FOREACH item IN ARRAY ARRAY['treasury_account','treasury_account_balance_pocket','bank_account_profile','cash_account_profile','digital_wallet_profile','settlement_account_profile','treasury_account_change_request','treasury_account_connection','statement_import_batch','external_statement','external_statement_line','statement_transaction_code_mapping','reconciliation_rule_set','reconciliation_rule_version','reconciliation_session','reconciliation_match','reconciliation_match_item','reconciliation_adjustment','cash_count','cash_count_denomination','cash_difference_resolution','cash_custodian_history','cash_position_projection','cash_forecast_version','cash_forecast_item'] LOOP
        EXECUTE format('ALTER TABLE finance_banking.%I ENABLE ROW LEVEL SECURITY', item);
        EXECUTE format('ALTER TABLE finance_banking.%I FORCE ROW LEVEL SECURITY', item);
        EXECUTE format('CREATE POLICY tenant_isolation ON finance_banking.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE ON finance_banking.%I TO erp_app', item);
    END LOOP;
END $$;

-- The compatibility backfill is migration-owned. Force RLS is restored immediately
-- after it so runtime roles can never use this bypass.
ALTER TABLE finance_banking.treasury_account NO FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_payments.payment_account NO FORCE ROW LEVEL SECURITY;

-- Compatibility bridge: the legacy Payments account identifier is preserved, while
-- every real monetary account receives the same stable canonical TreasuryAccount id.
ALTER TABLE finance_payments.payment_account ADD COLUMN IF NOT EXISTS treasury_account_id uuid;
INSERT INTO finance_banking.treasury_account(tenant_id,legal_entity_id,id,branch_id,code,name,account_type,status,currency_policy,currency_code,purpose,liquidity_class,allowed_uses,masked_identifier,secret_ref,version,created_at,updated_at)
SELECT p.tenant_id,p.legal_entity_id,p.id,p.branch_id,p.code,p.name,
       CASE p.account_type WHEN 'Bank' THEN 'Bank' WHEN 'Cashbox' THEN 'Cash' WHEN 'Wallet' THEN 'DigitalWallet' WHEN 'ProviderClearing' THEN 'ProviderSettlement' END,
       CASE p.status WHEN 'Archived' THEN 'Closed' ELSE p.status END,p.currency_policy,p.currency_code,'Migrated from Payments','Operational','[]'::jsonb,p.masked_identifier,p.secret_ref,p.version,p.created_at,p.updated_at
FROM finance_payments.payment_account p
WHERE p.account_type <> 'InternalClearing'
ON CONFLICT (tenant_id,id) DO NOTHING;
UPDATE finance_payments.payment_account p SET treasury_account_id=p.id WHERE p.account_type <> 'InternalClearing' AND p.treasury_account_id IS NULL;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_payment_account_treasury_account') THEN
        ALTER TABLE finance_payments.payment_account ADD CONSTRAINT fk_payment_account_treasury_account FOREIGN KEY (tenant_id,legal_entity_id,treasury_account_id) REFERENCES finance_banking.treasury_account(tenant_id,legal_entity_id,id);
    END IF;
END $$;
CREATE INDEX IF NOT EXISTS ix_payment_account_treasury_ref ON finance_payments.payment_account(tenant_id,legal_entity_id,treasury_account_id);
GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA finance_banking TO erp_app;

ALTER TABLE finance_banking.treasury_account FORCE ROW LEVEL SECURITY;
ALTER TABLE finance_payments.payment_account FORCE ROW LEVEL SECURITY;

RESET row_security;
RESET search_path;
