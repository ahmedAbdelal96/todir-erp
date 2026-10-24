SET search_path = finance_ledger, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS finance_ledger AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA finance_ledger FROM PUBLIC;
GRANT USAGE ON SCHEMA finance_ledger TO erp_app;

CREATE TABLE finance_ledger.account_category (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    account_type text NOT NULL,
    normal_balance text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (account_type IN ('Asset','Liability','Equity','Revenue','Expense')),
    CHECK (normal_balance IN ('Debit','Credit')),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE finance_ledger.account (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    category_id uuid,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    account_type text NOT NULL,
    subtype text NOT NULL,
    normal_balance text NOT NULL,
    parent_account_id uuid,
    allow_direct_posting boolean NOT NULL DEFAULT true,
    currency_policy text NOT NULL DEFAULT 'BaseOnly',
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, category_id) REFERENCES finance_ledger.account_category(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, parent_account_id) REFERENCES finance_ledger.account(tenant_id, legal_entity_id, id),
    CHECK (account_type IN ('Asset','Liability','Equity','Revenue','Expense')),
    CHECK (normal_balance IN ('Debit','Credit')),
    CHECK (currency_policy IN ('BaseOnly','AnyEnabled')),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE finance_ledger.journal (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    journal_type text NOT NULL,
    numbering_policy_ref text NOT NULL DEFAULT 'ledger-entry',
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (journal_type IN ('Sales','Purchases','Bank','Cash','Inventory','Payroll','General','Opening','Fx','Tax')),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE finance_ledger.fiscal_year (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    status text NOT NULL DEFAULT 'Open',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (start_date <= end_date),
    CHECK (status IN ('Open','Closed'))
);

CREATE TABLE finance_ledger.accounting_period (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    fiscal_year_id uuid NOT NULL,
    code text NOT NULL,
    start_date date NOT NULL,
    end_date date NOT NULL,
    sequence_no integer NOT NULL,
    status text NOT NULL DEFAULT 'Open',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, fiscal_year_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, fiscal_year_id) REFERENCES finance_ledger.fiscal_year(tenant_id, legal_entity_id, id),
    CHECK (start_date <= end_date),
    CHECK (sequence_no > 0),
    CHECK (status IN ('Open','SoftClosed','Closed'))
);

CREATE TABLE finance_ledger.financial_lock (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    lock_type text NOT NULL,
    start_date date NOT NULL,
    end_date date,
    status text NOT NULL DEFAULT 'Active',
    reason text NOT NULL,
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (end_date IS NULL OR start_date <= end_date),
    CHECK (lock_type IN ('General','Tax','Advisor','Module')),
    CHECK (status IN ('Active','Released'))
);

CREATE TABLE finance_ledger.accounting_configuration_version (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    version_number bigint NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    status text NOT NULL DEFAULT 'Active',
    snapshot_hash text NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, version_number),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (effective_to IS NULL OR effective_from <= effective_to),
    CHECK (status IN ('Draft','Active','Retired'))
);

CREATE TABLE finance_ledger.accounting_fact (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    fact_type text NOT NULL,
    fact_version integer NOT NULL,
    source_module text NOT NULL,
    source_reference text NOT NULL,
    idempotency_key text NOT NULL,
    business_date date NOT NULL,
    posting_date date NOT NULL,
    payload_jsonb jsonb NOT NULL,
    payload_hash text NOT NULL,
    status text NOT NULL DEFAULT 'Received',
    posted_entry_id uuid,
    correlation_id uuid NOT NULL,
    actor_id uuid,
    received_at timestamptz NOT NULL,
    posted_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, source_module, source_reference, fact_type, fact_version),
    UNIQUE (tenant_id, legal_entity_id, idempotency_key),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (fact_version > 0),
    CHECK (status IN ('Received','Posted','Rejected'))
);

CREATE TABLE finance_ledger.posting_rule (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    fact_type text NOT NULL,
    fact_version integer NOT NULL,
    code text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, fact_type, fact_version, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (status IN ('Draft','Active','Retired'))
);

CREATE TABLE finance_ledger.posting_rule_version (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    posting_rule_id uuid NOT NULL,
    version_number bigint NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    status text NOT NULL DEFAULT 'Active',
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, posting_rule_id, version_number),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, posting_rule_id) REFERENCES finance_ledger.posting_rule(tenant_id, legal_entity_id, id),
    CHECK (effective_to IS NULL OR effective_from <= effective_to),
    CHECK (status IN ('Draft','Active','Retired'))
);

CREATE TABLE finance_ledger.posting_rule_leg (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    posting_rule_version_id uuid NOT NULL,
    sequence_no integer NOT NULL,
    semantic_role text NOT NULL,
    amount_expression text NOT NULL,
    direction text NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, posting_rule_version_id, sequence_no),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, posting_rule_version_id) REFERENCES finance_ledger.posting_rule_version(tenant_id, legal_entity_id, id),
    CHECK (sequence_no > 0),
    CHECK (direction IN ('Debit','Credit'))
);

CREATE TABLE finance_ledger.account_role (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE finance_ledger.account_mapping (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    role_id uuid NOT NULL,
    code text NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    UNIQUE (tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, role_id) REFERENCES finance_ledger.account_role(tenant_id, legal_entity_id, id),
    CHECK (status IN ('Active','Archived'))
);

CREATE TABLE finance_ledger.account_mapping_version (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    mapping_id uuid NOT NULL,
    version_number bigint NOT NULL,
    effective_from date NOT NULL,
    effective_to date,
    product_category_code text,
    party_group_code text,
    tax_code text,
    journal_code text,
    specificity_rank integer NOT NULL,
    target_account_id uuid NOT NULL,
    status text NOT NULL DEFAULT 'Active',
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, mapping_id) REFERENCES finance_ledger.account_mapping(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, target_account_id) REFERENCES finance_ledger.account(tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, mapping_id, version_number),
    CHECK (specificity_rank >= 0),
    CHECK (effective_to IS NULL OR effective_from <= effective_to),
    CHECK (status IN ('Draft','Active','Retired'))
);

CREATE TABLE finance_ledger.journal_entry (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    journal_id uuid NOT NULL,
    fact_id uuid NOT NULL,
    source_module text NOT NULL,
    source_reference text NOT NULL,
    entry_number text NOT NULL,
    posting_date date NOT NULL,
    accounting_period_id uuid NOT NULL,
    fact_type text NOT NULL,
    fact_version integer NOT NULL,
    status text NOT NULL DEFAULT 'Posted',
    currency_code text NOT NULL,
    base_currency_code text NOT NULL,
    posting_rule_version_id uuid NOT NULL,
    accounting_configuration_version_id uuid,
    description text NOT NULL,
    correlation_id uuid NOT NULL,
    actor_id uuid,
    posted_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, entry_number),
    UNIQUE (tenant_id, fact_id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, journal_id) REFERENCES finance_ledger.journal(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, fact_id) REFERENCES finance_ledger.accounting_fact(tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, accounting_period_id) REFERENCES finance_ledger.accounting_period(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, posting_rule_version_id) REFERENCES finance_ledger.posting_rule_version(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, accounting_configuration_version_id) REFERENCES finance_ledger.accounting_configuration_version(tenant_id, legal_entity_id, id),
    UNIQUE (tenant_id, legal_entity_id, id),
    CHECK (status = 'Posted'),
    CHECK (fact_version > 0)
);

ALTER TABLE finance_ledger.accounting_fact ADD CONSTRAINT accounting_fact_posted_entry_fk FOREIGN KEY (tenant_id, posted_entry_id) REFERENCES finance_ledger.journal_entry(tenant_id, id);

CREATE TABLE finance_ledger.journal_entry_line (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    journal_entry_id uuid NOT NULL,
    sequence_no integer NOT NULL,
    account_id uuid NOT NULL,
    debit numeric(28,8) NOT NULL DEFAULT 0,
    credit numeric(28,8) NOT NULL DEFAULT 0,
    currency_code text NOT NULL,
    amount numeric(28,8) NOT NULL,
    exchange_rate numeric(28,12),
    semantic_role text NOT NULL,
    description text,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, journal_entry_id, sequence_no),
    FOREIGN KEY (tenant_id, legal_entity_id, journal_entry_id) REFERENCES finance_ledger.journal_entry(tenant_id, legal_entity_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id, account_id) REFERENCES finance_ledger.account(tenant_id, legal_entity_id, id),
    CHECK (sequence_no > 0),
    CHECK (debit >= 0 AND credit >= 0),
    CHECK (NOT (debit > 0 AND credit > 0)),
    CHECK (debit > 0 OR credit > 0),
    CHECK (amount > 0),
    CHECK (exchange_rate IS NULL OR exchange_rate > 0)
);

CREATE INDEX accounting_period_date_idx ON finance_ledger.accounting_period (tenant_id, legal_entity_id, start_date, end_date);
CREATE INDEX financial_lock_date_idx ON finance_ledger.financial_lock (tenant_id, legal_entity_id, start_date, end_date);
CREATE INDEX accounting_fact_status_idx ON finance_ledger.accounting_fact (tenant_id, legal_entity_id, status, received_at);
CREATE INDEX journal_entry_posting_date_idx ON finance_ledger.journal_entry (tenant_id, legal_entity_id, posting_date);
CREATE INDEX journal_entry_line_account_idx ON finance_ledger.journal_entry_line (tenant_id, legal_entity_id, account_id);

CREATE OR REPLACE FUNCTION finance_ledger.validate_account_parent() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF NEW.parent_account_id IS NULL THEN RETURN NEW; END IF;
    IF NEW.parent_account_id = NEW.id THEN RAISE EXCEPTION 'ledger.account_parent_cycle' USING ERRCODE = '23514'; END IF;
    IF EXISTS (WITH RECURSIVE ancestors(id) AS (
        SELECT parent_account_id FROM finance_ledger.account WHERE tenant_id = NEW.tenant_id AND legal_entity_id = NEW.legal_entity_id AND id = NEW.parent_account_id
        UNION ALL
        SELECT a.parent_account_id FROM finance_ledger.account a JOIN ancestors x ON a.id = x.id WHERE a.tenant_id = NEW.tenant_id AND a.legal_entity_id = NEW.legal_entity_id AND a.parent_account_id IS NOT NULL
    ) SELECT 1 FROM ancestors WHERE id = NEW.id) THEN RAISE EXCEPTION 'ledger.account_parent_cycle' USING ERRCODE = '23514'; END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER account_parent_validation BEFORE INSERT OR UPDATE OF parent_account_id ON finance_ledger.account FOR EACH ROW EXECUTE FUNCTION finance_ledger.validate_account_parent();

CREATE OR REPLACE FUNCTION finance_ledger.prevent_period_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM finance_ledger.accounting_period p WHERE p.tenant_id = NEW.tenant_id AND p.legal_entity_id = NEW.legal_entity_id AND p.fiscal_year_id = NEW.fiscal_year_id AND p.id <> NEW.id AND daterange(p.start_date, p.end_date, '[]') && daterange(NEW.start_date, NEW.end_date, '[]')) THEN
        RAISE EXCEPTION 'ledger.period_overlap' USING ERRCODE = '23P01';
    END IF;
    RETURN NEW;
END; $$;
CREATE TRIGGER accounting_period_no_overlap BEFORE INSERT OR UPDATE OF start_date, end_date, fiscal_year_id ON finance_ledger.accounting_period FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_period_overlap();

CREATE OR REPLACE FUNCTION finance_ledger.prevent_posted_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    RAISE EXCEPTION 'ledger.posted_entry_immutable' USING ERRCODE = '55000';
END; $$;
CREATE TRIGGER journal_entry_immutable BEFORE UPDATE OR DELETE ON finance_ledger.journal_entry FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_posted_mutation();
CREATE TRIGGER journal_entry_line_immutable BEFORE UPDATE OR DELETE ON finance_ledger.journal_entry_line FOR EACH ROW EXECUTE FUNCTION finance_ledger.prevent_posted_mutation();

ALTER TABLE finance_ledger.account_category ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.fiscal_year ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.accounting_period ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.financial_lock ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.accounting_configuration_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.accounting_fact ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.posting_rule_leg ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_role ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_mapping ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.account_mapping_version ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal_entry ENABLE ROW LEVEL SECURITY;
ALTER TABLE finance_ledger.journal_entry_line ENABLE ROW LEVEL SECURITY;

DO $$ DECLARE table_name text; BEGIN
    FOREACH table_name IN ARRAY ARRAY['account_category','account','journal','fiscal_year','accounting_period','financial_lock','accounting_configuration_version','accounting_fact','posting_rule','posting_rule_version','posting_rule_leg','account_role','account_mapping','account_mapping_version','journal_entry','journal_entry_line'] LOOP
        EXECUTE format('CREATE POLICY %I_tenant_policy ON finance_ledger.%I USING (current_setting(''app.tenant_id'', true) IS NOT NULL AND tenant_id = current_setting(''app.tenant_id'', true)::uuid) WITH CHECK (current_setting(''app.tenant_id'', true) IS NOT NULL AND tenant_id = current_setting(''app.tenant_id'', true)::uuid)', table_name, table_name);
    END LOOP;
END $$;

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA finance_ledger TO erp_app;
