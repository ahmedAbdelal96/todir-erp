-- Payroll workflow closure: run lineage, loan actions, imports and opening balances.
-- This is a forward migration; previously applied scripts remain immutable.

ALTER TABLE business_payroll.payroll_run
    ADD COLUMN IF NOT EXISTS source_run_id uuid NULL,
    ADD COLUMN IF NOT EXISTS source_period_id uuid NULL,
    ADD COLUMN IF NOT EXISTS settlement_employment_id uuid NULL,
    ADD COLUMN IF NOT EXISTS effective_date date NULL,
    ADD COLUMN IF NOT EXISTS reason_code varchar(80) NULL,
    ADD COLUMN IF NOT EXISTS notes varchar(500) NULL;

DO $$
DECLARE item text;
BEGIN
    SELECT conname INTO item FROM pg_constraint
    WHERE conrelid='business_payroll.payroll_run'::regclass AND contype='c' AND pg_get_constraintdef(oid) LIKE '%run_type%';
    IF item IS NOT NULL THEN EXECUTE format('ALTER TABLE business_payroll.payroll_run DROP CONSTRAINT %I', item); END IF;
END $$;
ALTER TABLE business_payroll.payroll_run ADD CONSTRAINT payroll_run_type_allowed CHECK (run_type IN ('Regular','OffCycle','Retro','Retroactive','Adjustment','BonusOnly','FinalSettlement'));

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_constraint WHERE conrelid='business_payroll.payroll_run'::regclass AND conname='payroll_run_tenant_id_payroll_group_id_period_id_run_type_key') THEN
        ALTER TABLE business_payroll.payroll_run DROP CONSTRAINT payroll_run_tenant_id_payroll_group_id_period_id_run_type_key;
    END IF;
END $$;

CREATE UNIQUE INDEX IF NOT EXISTS ux_payroll_regular_run
    ON business_payroll.payroll_run(tenant_id, payroll_group_id, period_id)
    WHERE run_type='Regular';
CREATE UNIQUE INDEX IF NOT EXISTS ux_payroll_derived_run
    ON business_payroll.payroll_run(tenant_id, payroll_group_id, run_type, coalesce(source_run_id, '00000000-0000-0000-0000-000000000000'::uuid), coalesce(settlement_employment_id, '00000000-0000-0000-0000-000000000000'::uuid), coalesce(period_id, '00000000-0000-0000-0000-000000000000'::uuid));
CREATE INDEX IF NOT EXISTS ix_payroll_run_lineage ON business_payroll.payroll_run(tenant_id, legal_entity_id, run_type, source_run_id, effective_date);

CREATE TABLE IF NOT EXISTS business_payroll.payroll_loan_action (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, receivable_id uuid NOT NULL,
    action varchar(30) NOT NULL CHECK (action IN ('Pause','Resume','Reschedule','ExternalRepayment','EarlySettlement','WriteOff')),
    amount numeric(19,4) NULL CHECK (amount IS NULL OR amount > 0), effective_date date NOT NULL,
    new_installment_amount numeric(19,4) NULL CHECK (new_installment_amount IS NULL OR new_installment_amount > 0),
    new_due_date date NULL, external_reference varchar(200) NULL, reason varchar(500) NULL,
    actor_id uuid NOT NULL, idempotency_key varchar(200) NOT NULL, status varchar(20) NOT NULL DEFAULT 'Applied' CHECK (status IN ('Applied','Rejected')),
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,idempotency_key)
);

CREATE TABLE IF NOT EXISTS business_payroll.payroll_import_batch (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, import_type varchar(40) NOT NULL,
    source_system varchar(120) NOT NULL, batch_reference varchar(160) NOT NULL, file_sha256 varchar(128) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Previewed' CHECK (status IN ('Previewed','Committed','Rejected')),
    row_count integer NOT NULL DEFAULT 0, committed_at timestamptz NULL, actor_id uuid NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,import_type,source_system,batch_reference,file_sha256)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_import_row (
    tenant_id uuid NOT NULL, id uuid NOT NULL, batch_id uuid NOT NULL, row_number integer NOT NULL,
    row_key varchar(200) NOT NULL, row_data jsonb NOT NULL, status varchar(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending','Imported','Rejected')),
    issue_code varchar(120) NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,batch_id,row_number)
);

CREATE TABLE IF NOT EXISTS business_payroll.payroll_opening_balance (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, employment_id uuid NOT NULL,
    balance_type varchar(30) NOT NULL CHECK (balance_type IN ('NetPay','Tax','Insurance','Loan','Advance','Other')),
    amount numeric(19,4) NOT NULL, currency_code char(3) NOT NULL, as_of_date date NOT NULL,
    source_reference varchar(200) NOT NULL, status varchar(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft','Approved','Posted','Cancelled')),
    actor_id uuid NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,employment_id,balance_type,as_of_date,source_reference)
);

DO $$
DECLARE item text;
BEGIN
  FOREACH item IN ARRAY ARRAY['payroll_loan_action','payroll_import_batch','payroll_import_row','payroll_opening_balance'] LOOP
    EXECUTE format('ALTER TABLE business_payroll.%I ENABLE ROW LEVEL SECURITY', item);
    EXECUTE format('ALTER TABLE business_payroll.%I FORCE ROW LEVEL SECURITY', item);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON business_payroll.%I', item);
    EXECUTE format('CREATE POLICY tenant_isolation ON business_payroll.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
  END LOOP;
END $$;

CREATE INDEX IF NOT EXISTS ix_payroll_loan_action_receivable ON business_payroll.payroll_loan_action(tenant_id, receivable_id, effective_date DESC);
CREATE INDEX IF NOT EXISTS ix_payroll_import_batch_status ON business_payroll.payroll_import_batch(tenant_id, legal_entity_id, import_type, status, created_at DESC);
CREATE INDEX IF NOT EXISTS ix_payroll_opening_balance_status ON business_payroll.payroll_opening_balance(tenant_id, legal_entity_id, status, as_of_date);
GRANT SELECT, INSERT, UPDATE, DELETE ON business_payroll.payroll_loan_action, business_payroll.payroll_import_batch, business_payroll.payroll_import_row, business_payroll.payroll_opening_balance TO erp_app;
