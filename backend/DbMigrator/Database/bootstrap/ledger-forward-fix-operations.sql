-- Pass 2: reversible postings, dimensions, Ledger-owned application workflows,
-- and rebuildable accountant read models. All tables remain Ledger-owned.

-- A previously finalized database may already FORCE RLS on the baseline table.
-- The migration owner must temporarily relax only this table while copying the
-- accepted Pass 1 amounts into the explicit Pass 2 currency columns.
ALTER TABLE finance_ledger.journal_entry_line NO FORCE ROW LEVEL SECURITY;
DROP TRIGGER IF EXISTS journal_entry_line_immutable ON finance_ledger.journal_entry_line;
DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'journal_entry_line_tenant_entity_id_uq') THEN ALTER TABLE finance_ledger.journal_entry_line ADD CONSTRAINT journal_entry_line_tenant_entity_id_uq UNIQUE (tenant_id, legal_entity_id, id); END IF;
END $$;

ALTER TABLE finance_ledger.journal_entry_line
    ADD COLUMN IF NOT EXISTS transaction_debit numeric(28,8) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS transaction_credit numeric(28,8) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS base_debit numeric(28,8) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS base_credit numeric(28,8) NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS exchange_rate_snapshot jsonb,
    ADD COLUMN IF NOT EXISTS line_reference text;

UPDATE finance_ledger.journal_entry_line
SET transaction_debit = debit,
    transaction_credit = credit,
    base_debit = debit,
    base_credit = credit
WHERE transaction_debit = 0 AND transaction_credit = 0;

CREATE TRIGGER journal_entry_line_immutable BEFORE UPDATE OR DELETE ON finance_ledger.journal_entry_line FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_posted_mutation();

CREATE TABLE IF NOT EXISTS finance_ledger.journal_entry_reversal (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    original_entry_id uuid NOT NULL,
    reversal_entry_id uuid NOT NULL,
    reason text NOT NULL,
    actor_id uuid NOT NULL,
    reversal_date date NOT NULL,
    auto_reversal_date date,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, original_entry_id),
    UNIQUE (tenant_id, legal_entity_id, reversal_entry_id),
    FOREIGN KEY (tenant_id, legal_entity_id, original_entry_id) REFERENCES finance_ledger.journal_entry(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, reversal_entry_id) REFERENCES finance_ledger.journal_entry(tenant_id, legal_entity_id, id),
    CHECK (length(trim(reason)) > 0)
);

CREATE TABLE IF NOT EXISTS finance_ledger.dimension_definition (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    value_owner_module text NOT NULL DEFAULT 'ledger',
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE IF NOT EXISTS finance_ledger.dimension_value (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    dimension_definition_id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    external_owner_module text,
    external_reference_id uuid,
    status text NOT NULL DEFAULT 'Active',
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, dimension_definition_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, dimension_definition_id) REFERENCES finance_ledger.dimension_definition(tenant_id, legal_entity_id, id),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE IF NOT EXISTS finance_ledger.dimension_policy (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    dimension_definition_id uuid NOT NULL,
    scope_type text NOT NULL,
    scope_reference text NOT NULL,
    requirement text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, dimension_definition_id, scope_type, scope_reference),
    FOREIGN KEY (tenant_id, legal_entity_id, dimension_definition_id) REFERENCES finance_ledger.dimension_definition(tenant_id, legal_entity_id, id),
    CHECK (scope_type IN ('Account','AccountType','Journal','FactType')),
    CHECK (requirement IN ('Required','Optional','Forbidden'))
);

CREATE TABLE IF NOT EXISTS finance_ledger.journal_line_dimension (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    journal_entry_line_id uuid NOT NULL,
    dimension_definition_id uuid NOT NULL,
    dimension_value_id uuid NOT NULL,
    PRIMARY KEY (tenant_id, journal_entry_line_id, dimension_definition_id),
    FOREIGN KEY (tenant_id, legal_entity_id, journal_entry_line_id) REFERENCES finance_ledger.journal_entry_line(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, dimension_definition_id) REFERENCES finance_ledger.dimension_definition(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, dimension_value_id) REFERENCES finance_ledger.dimension_value(tenant_id, legal_entity_id, id)
);

CREATE TABLE IF NOT EXISTS finance_ledger.general_journal_draft (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    journal_id uuid NOT NULL,
    posting_date date NOT NULL,
    description text NOT NULL,
    base_currency_code text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    idempotency_key text NOT NULL,
    created_by uuid NOT NULL,
    submitted_by uuid,
    approved_by uuid,
    posted_by uuid,
    posted_entry_id uuid,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id, journal_id) REFERENCES finance_ledger.journal(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, posted_entry_id) REFERENCES finance_ledger.journal_entry(tenant_id, legal_entity_id, id),
    CHECK (status IN ('Draft','Submitted','Approved','Posted'))
);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'general_journal_draft_tenant_entity_id_uq') THEN ALTER TABLE finance_ledger.general_journal_draft ADD CONSTRAINT general_journal_draft_tenant_entity_id_uq UNIQUE (tenant_id, legal_entity_id, id); END IF;
END $$;

CREATE TABLE IF NOT EXISTS finance_ledger.general_journal_draft_line (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    draft_id uuid NOT NULL,
    sequence_no integer NOT NULL,
    account_id uuid NOT NULL,
    debit numeric(28,8) NOT NULL DEFAULT 0,
    credit numeric(28,8) NOT NULL DEFAULT 0,
    transaction_currency_code text NOT NULL,
    transaction_debit numeric(28,8) NOT NULL DEFAULT 0,
    transaction_credit numeric(28,8) NOT NULL DEFAULT 0,
    base_debit numeric(28,8) NOT NULL DEFAULT 0,
    base_credit numeric(28,8) NOT NULL DEFAULT 0,
    exchange_rate numeric(28,12) NOT NULL DEFAULT 1,
    description text,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, draft_id, sequence_no),
    FOREIGN KEY (tenant_id, legal_entity_id, draft_id) REFERENCES finance_ledger.general_journal_draft(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, account_id) REFERENCES finance_ledger.account(tenant_id, legal_entity_id, id),
    CHECK (debit >= 0 AND credit >= 0 AND NOT (debit > 0 AND credit > 0) AND (debit > 0 OR credit > 0)),
    CHECK (base_debit >= 0 AND base_credit >= 0 AND NOT (base_debit > 0 AND base_credit > 0) AND (base_debit > 0 OR base_credit > 0))
);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'general_journal_draft_line_tenant_entity_id_uq') THEN ALTER TABLE finance_ledger.general_journal_draft_line ADD CONSTRAINT general_journal_draft_line_tenant_entity_id_uq UNIQUE (tenant_id, legal_entity_id, id); END IF;
END $$;

CREATE TABLE IF NOT EXISTS finance_ledger.general_journal_draft_line_dimension (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    draft_line_id uuid NOT NULL,
    dimension_definition_id uuid NOT NULL,
    dimension_value_id uuid NOT NULL,
    PRIMARY KEY (tenant_id, draft_line_id, dimension_definition_id),
    FOREIGN KEY (tenant_id, legal_entity_id, draft_line_id) REFERENCES finance_ledger.general_journal_draft_line(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, dimension_definition_id) REFERENCES finance_ledger.dimension_definition(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, dimension_value_id) REFERENCES finance_ledger.dimension_value(tenant_id, legal_entity_id, id)
);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'general_journal_draft_line_tenant_entity_id_uq') THEN ALTER TABLE finance_ledger.general_journal_draft_line ADD CONSTRAINT general_journal_draft_line_tenant_entity_id_uq UNIQUE (tenant_id, legal_entity_id, id); END IF;
END $$;

CREATE TABLE IF NOT EXISTS finance_ledger.opening_balance_batch (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    batch_identity text NOT NULL,
    file_hash text NOT NULL,
    legacy_system text,
    go_live_date date NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    created_by uuid NOT NULL,
    posted_entry_id uuid,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, batch_identity),
    UNIQUE (tenant_id, legal_entity_id, file_hash),
    FOREIGN KEY (tenant_id, legal_entity_id, posted_entry_id) REFERENCES finance_ledger.journal_entry(tenant_id, legal_entity_id, id),
    CHECK (status IN ('Draft','Validated','Posted','Locked'))
);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'opening_balance_batch_tenant_entity_id_uq') THEN ALTER TABLE finance_ledger.opening_balance_batch ADD CONSTRAINT opening_balance_batch_tenant_entity_id_uq UNIQUE (tenant_id, legal_entity_id, id); END IF;
END $$;

CREATE TABLE IF NOT EXISTS finance_ledger.opening_balance_line (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    batch_id uuid NOT NULL,
    sequence_no integer NOT NULL,
    account_id uuid NOT NULL,
    debit numeric(28,8) NOT NULL DEFAULT 0,
    credit numeric(28,8) NOT NULL DEFAULT 0,
    currency_code text NOT NULL,
    base_debit numeric(28,8) NOT NULL DEFAULT 0,
    base_credit numeric(28,8) NOT NULL DEFAULT 0,
    exchange_rate numeric(28,12) NOT NULL DEFAULT 1,
    legacy_account_code text,
    legacy_reference text,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, batch_id, sequence_no),
    FOREIGN KEY (tenant_id, legal_entity_id, batch_id) REFERENCES finance_ledger.opening_balance_batch(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, account_id) REFERENCES finance_ledger.account(tenant_id, legal_entity_id, id),
    CHECK (debit >= 0 AND credit >= 0 AND NOT (debit > 0 AND credit > 0) AND (debit > 0 OR credit > 0)),
    CHECK (base_debit >= 0 AND base_credit >= 0 AND NOT (base_debit > 0 AND base_credit > 0) AND (base_debit > 0 OR base_credit > 0))
);

DO $$ BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'opening_balance_batch_tenant_entity_id_uq') THEN ALTER TABLE finance_ledger.opening_balance_batch ADD CONSTRAINT opening_balance_batch_tenant_entity_id_uq UNIQUE (tenant_id, legal_entity_id, id); END IF;
END $$;

CREATE TABLE IF NOT EXISTS finance_ledger.account_period_balance (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    account_id uuid NOT NULL,
    fiscal_year_id uuid NOT NULL,
    accounting_period_id uuid NOT NULL,
    dimension_key text NOT NULL DEFAULT '',
    currency_code text NOT NULL,
    opening_debit numeric(28,8) NOT NULL DEFAULT 0,
    opening_credit numeric(28,8) NOT NULL DEFAULT 0,
    period_debit numeric(28,8) NOT NULL DEFAULT 0,
    period_credit numeric(28,8) NOT NULL DEFAULT 0,
    closing_balance numeric(28,8) NOT NULL DEFAULT 0,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id, account_id, accounting_period_id, dimension_key, currency_code),
    FOREIGN KEY (tenant_id, legal_entity_id, account_id) REFERENCES finance_ledger.account(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, fiscal_year_id) REFERENCES finance_ledger.fiscal_year(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, accounting_period_id) REFERENCES finance_ledger.accounting_period(tenant_id, legal_entity_id, id)
);

CREATE INDEX IF NOT EXISTS journal_entry_reversal_original_idx ON finance_ledger.journal_entry_reversal (tenant_id, legal_entity_id, original_entry_id);
CREATE INDEX IF NOT EXISTS journal_line_dimension_value_idx ON finance_ledger.journal_line_dimension (tenant_id, legal_entity_id, dimension_value_id);
CREATE INDEX IF NOT EXISTS journal_entry_line_report_idx ON finance_ledger.journal_entry_line (tenant_id, legal_entity_id, account_id, journal_entry_id);
CREATE INDEX IF NOT EXISTS account_period_balance_report_idx ON finance_ledger.account_period_balance (tenant_id, legal_entity_id, accounting_period_id, account_id);
CREATE INDEX IF NOT EXISTS opening_batch_status_idx ON finance_ledger.opening_balance_batch (tenant_id, legal_entity_id, status);


DO $$ DECLARE table_name text; BEGIN
    FOREACH table_name IN ARRAY ARRAY['journal_entry_reversal','dimension_definition','dimension_value','dimension_policy','journal_line_dimension','general_journal_draft','general_journal_draft_line','general_journal_draft_line_dimension','opening_balance_batch','opening_balance_line','account_period_balance'] LOOP
        EXECUTE format('ALTER TABLE finance_ledger.%I ENABLE ROW LEVEL SECURITY', table_name);
        EXECUTE format('DROP POLICY IF EXISTS %I_tenant_policy ON finance_ledger.%I', table_name, table_name);
        EXECUTE format('CREATE POLICY %I_tenant_policy ON finance_ledger.%I USING (current_setting(''app.tenant_id'', true) IS NOT NULL AND tenant_id = current_setting(''app.tenant_id'', true)::uuid) WITH CHECK (current_setting(''app.tenant_id'', true) IS NOT NULL AND tenant_id = current_setting(''app.tenant_id'', true)::uuid)', table_name, table_name, table_name);
        EXECUTE format('GRANT SELECT, INSERT, UPDATE ON finance_ledger.%I TO erp_app', table_name);
    END LOOP;
END $$;

GRANT SELECT, INSERT, UPDATE ON finance_ledger.journal_entry_line TO erp_app;
