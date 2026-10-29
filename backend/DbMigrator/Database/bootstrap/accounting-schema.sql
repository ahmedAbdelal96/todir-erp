SET search_path = business_accounting, public;
SET row_security = off;

CREATE SCHEMA IF NOT EXISTS business_accounting AUTHORIZATION erp_owner;
REVOKE ALL ON SCHEMA business_accounting FROM PUBLIC;
GRANT USAGE ON SCHEMA business_accounting TO erp_app;

CREATE TABLE business_accounting.accounting_setup (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    mode text NOT NULL DEFAULT 'Managed',
    go_live_status text NOT NULL DEFAULT 'NotStarted',
    configuration_version_id uuid,
    row_version bigint NOT NULL DEFAULT 1,
    updated_by uuid NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, legal_entity_id),
    FOREIGN KEY (tenant_id) REFERENCES platform.tenant(id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (mode IN ('Managed','Full')),
    CHECK (go_live_status IN ('NotStarted','Ready','Approved','Blocked')),
    CHECK (row_version > 0)
);

CREATE TABLE business_accounting.reconciliation_run (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    reconciliation_type text NOT NULL,
    as_of_date date NOT NULL,
    source_balance numeric(20,6),
    ledger_balance numeric(20,6),
    difference numeric(20,6),
    currency_code char(3),
    status text NOT NULL,
    source_version text,
    diagnostics jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_by uuid NOT NULL,
    correlation_id uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, reconciliation_type, as_of_date, created_at, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (reconciliation_type IN ('AR','AP','Inventory','LedgerProjection')),
    CHECK (status IN ('Reconciled','Difference','NotReady','NotApplicable','Error')),
    CHECK (currency_code IS NULL OR currency_code ~ '^[A-Z]{3}$'),
    CHECK (jsonb_typeof(diagnostics) = 'array')
);

CREATE TABLE business_accounting.reconciliation_issue (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    reconciliation_run_id uuid NOT NULL,
    code text NOT NULL,
    severity text NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    status text NOT NULL DEFAULT 'Open',
    created_at timestamptz NOT NULL,
    resolved_at timestamptz,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, reconciliation_run_id) REFERENCES business_accounting.reconciliation_run(tenant_id, id),
    CHECK (severity IN ('Attention','Blocking','Critical')),
    CHECK (status IN ('Open','Resolved')),
    CHECK (jsonb_typeof(details) = 'object')
);

CREATE TABLE business_accounting.close_run (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    period_id uuid NOT NULL,
    idempotency_key text NOT NULL,
    payload_hash char(64) NOT NULL,
    status text NOT NULL,
    requested_by uuid NOT NULL,
    reason text,
    completed_at timestamptz,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    row_version bigint NOT NULL DEFAULT 1,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, period_id, idempotency_key),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (status IN ('Previewed','Blocked','Closed','Reopened')),
    CHECK (length(btrim(idempotency_key)) BETWEEN 1 AND 200),
    CHECK (payload_hash ~ '^[0-9a-f]{64}$'),
    CHECK (row_version > 0)
);

CREATE TABLE business_accounting.close_check (
    tenant_id uuid NOT NULL,
    close_run_id uuid NOT NULL,
    id uuid NOT NULL,
    check_code text NOT NULL,
    severity text NOT NULL,
    status text NOT NULL,
    details jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, close_run_id, id),
    FOREIGN KEY (tenant_id, close_run_id) REFERENCES business_accounting.close_run(tenant_id, id),
    CHECK (severity IN ('Info','Warning','Blocking','Critical')),
    CHECK (status IN ('Pass','Warning','Fail','NotApplicable','NotReady')),
    CHECK (jsonb_typeof(details) = 'object')
);

CREATE TABLE business_accounting.financial_statement_definition (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    code text NOT NULL,
    statement_kind text NOT NULL,
    status text NOT NULL DEFAULT 'Draft',
    current_version integer NOT NULL DEFAULT 1,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    UNIQUE (tenant_id, legal_entity_id, code),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (statement_kind IN ('ProfitAndLoss','BalanceSheet')),
    CHECK (status IN ('Draft','Active','Archived')),
    CHECK (length(btrim(code)) BETWEEN 1 AND 100),
    CHECK (current_version > 0)
);

CREATE TABLE business_accounting.financial_statement_version (
    tenant_id uuid NOT NULL,
    definition_id uuid NOT NULL,
    version integer NOT NULL,
    status text NOT NULL,
    effective_from date,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, definition_id, version),
    FOREIGN KEY (tenant_id, definition_id) REFERENCES business_accounting.financial_statement_definition(tenant_id, id),
    CHECK (status IN ('Draft','Active','Superseded')),
    CHECK (version > 0)
);

CREATE TABLE business_accounting.financial_statement_line (
    tenant_id uuid NOT NULL,
    definition_id uuid NOT NULL,
    version integer NOT NULL,
    id uuid NOT NULL,
    parent_line_id uuid,
    sequence integer NOT NULL,
    code text NOT NULL,
    name_ar text NOT NULL,
    name_en text NOT NULL,
    line_kind text NOT NULL,
    account_type text,
    account_ids uuid[] NOT NULL DEFAULT '{}',
    arithmetic_operator text,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, definition_id, version, id),
    FOREIGN KEY (tenant_id, definition_id, version) REFERENCES business_accounting.financial_statement_version(tenant_id, definition_id, version),
    CHECK (sequence > 0),
    CHECK (line_kind IN ('MappedAccounts','Subtotal','Arithmetic')),
    CHECK (account_type IS NULL OR account_type IN ('Asset','Liability','Equity','Revenue','Expense')),
    CHECK (arithmetic_operator IS NULL OR arithmetic_operator IN ('Add','Subtract'))
);

CREATE TABLE business_accounting.report_snapshot_ref (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    report_kind text NOT NULL,
    as_of_date date NOT NULL,
    definition_id uuid,
    definition_version integer,
    source_cutoff timestamptz NOT NULL,
    evidence jsonb NOT NULL,
    created_by uuid NOT NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id, id),
    FOREIGN KEY (tenant_id, legal_entity_id) REFERENCES platform.legal_entity(tenant_id, id),
    CHECK (jsonb_typeof(evidence) = 'object')
);

DO $$ DECLARE item text; BEGIN
    FOREACH item IN ARRAY ARRAY['accounting_setup','reconciliation_run','reconciliation_issue','close_run','close_check','financial_statement_definition','financial_statement_version','financial_statement_line','report_snapshot_ref'] LOOP
        EXECUTE format('ALTER TABLE business_accounting.%I ENABLE ROW LEVEL SECURITY', item);
        EXECUTE format('ALTER TABLE business_accounting.%I FORCE ROW LEVEL SECURITY', item);
        EXECUTE format('CREATE POLICY tenant_isolation ON business_accounting.%I USING (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
    END LOOP;
END $$;

CREATE INDEX ix_accounting_reconciliation_scope ON business_accounting.reconciliation_run(tenant_id, legal_entity_id, reconciliation_type, as_of_date DESC, created_at DESC);
CREATE INDEX ix_accounting_reconciliation_issue_open ON business_accounting.reconciliation_issue(tenant_id, legal_entity_id, status, severity, created_at DESC);
CREATE INDEX ix_accounting_close_scope ON business_accounting.close_run(tenant_id, legal_entity_id, period_id, created_at DESC);
CREATE INDEX ix_accounting_statement_scope ON business_accounting.financial_statement_definition(tenant_id, legal_entity_id, statement_kind, status);

GRANT SELECT, INSERT, UPDATE ON ALL TABLES IN SCHEMA business_accounting TO erp_app;
