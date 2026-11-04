CREATE SCHEMA IF NOT EXISTS business_payroll AUTHORIZATION erp_owner;

CREATE TABLE IF NOT EXISTS business_payroll.payroll_group (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, code varchar(40) NOT NULL,
    name varchar(160) NOT NULL, frequency varchar(20) NOT NULL CHECK (frequency IN ('Monthly','Weekly','Biweekly','SemiMonthly')),
    pay_day smallint NOT NULL CHECK (pay_day BETWEEN 1 AND 31), cutoff_rule jsonb NOT NULL DEFAULT '{}'::jsonb,
    holiday_adjustment varchar(20) NOT NULL DEFAULT 'NextWorkingDay' CHECK (holiday_adjustment IN ('NextWorkingDay','PreviousWorkingDay','None')),
    currency_code char(3) NOT NULL, status varchar(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')),
    version integer NOT NULL DEFAULT 1, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_period (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, payroll_group_id uuid NOT NULL,
    period_start date NOT NULL, period_end date NOT NULL, input_cutoff date NOT NULL, calculation_date date NOT NULL,
    planned_pay_date date NOT NULL, actual_pay_date date NULL, status varchar(20) NOT NULL DEFAULT 'Open' CHECK (status IN ('Open','Processing','Finalized','Cancelled')),
    created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,payroll_group_id,period_start,period_end), CHECK (period_end >= period_start), CHECK (input_cutoff <= period_end)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_component (
    tenant_id uuid NOT NULL, id uuid NOT NULL, code varchar(50) NOT NULL, name varchar(160) NOT NULL,
    category varchar(30) NOT NULL CHECK (category IN ('Earning','EmployeeDeduction','EmployerContribution','Informational','Recovery')),
    direction varchar(10) NOT NULL CHECK (direction IN ('Credit','Debit','Memo')), input_mode varchar(20) NOT NULL CHECK (input_mode IN ('Formula','Manual','Provider')),
    status varchar(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Retired')), created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,code)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_component_version (
    tenant_id uuid NOT NULL, id uuid NOT NULL, component_id uuid NOT NULL, effective_from date NOT NULL, effective_to date NULL,
    formula jsonb NOT NULL DEFAULT '{}'::jsonb, rounding_scale smallint NOT NULL DEFAULT 2 CHECK (rounding_scale BETWEEN 0 AND 6),
    provider_code varchar(80) NULL, status varchar(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft','Published','Retired')),
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,component_id,effective_from), CHECK (effective_to IS NULL OR effective_to > effective_from)
);
CREATE TABLE IF NOT EXISTS business_payroll.salary_structure (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, code varchar(50) NOT NULL, name varchar(160) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Active' CHECK (status IN ('Active','Archived')), created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,legal_entity_id,code)
);
CREATE TABLE IF NOT EXISTS business_payroll.salary_structure_version (
    tenant_id uuid NOT NULL, id uuid NOT NULL, salary_structure_id uuid NOT NULL, version_number integer NOT NULL,
    effective_from date NOT NULL, effective_to date NULL, status varchar(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft','Published','Archived')),
    currency_code char(3) NOT NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,salary_structure_id,version_number)
);
CREATE TABLE IF NOT EXISTS business_payroll.salary_structure_component (
    tenant_id uuid NOT NULL, id uuid NOT NULL, structure_version_id uuid NOT NULL, component_id uuid NOT NULL,
    component_version_id uuid NOT NULL, priority smallint NOT NULL DEFAULT 100, default_amount numeric(19,4) NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,structure_version_id,component_id)
);
CREATE TABLE IF NOT EXISTS business_payroll.employee_payroll_assignment (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, employment_id uuid NOT NULL,
    payroll_group_id uuid NOT NULL, salary_structure_version_id uuid NOT NULL, effective_from date NOT NULL, effective_to date NULL,
    base_salary numeric(19,4) NOT NULL CHECK (base_salary >= 0), currency_code char(3) NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), CHECK (effective_to IS NULL OR effective_to > effective_from)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_input (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, employment_id uuid NOT NULL, component_id uuid NOT NULL,
    input_date date NOT NULL, amount numeric(19,4) NOT NULL, currency_code char(3) NOT NULL, source varchar(30) NOT NULL,
    status varchar(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft','Approved','Cancelled','Consumed')), idempotency_key varchar(200) NULL,
    notes varchar(500) NULL, created_at timestamptz NOT NULL, approved_at timestamptz NULL, consumed_run_id uuid NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,idempotency_key)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_run (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, payroll_group_id uuid NOT NULL, period_id uuid NULL,
    run_number varchar(60) NOT NULL, run_type varchar(20) NOT NULL CHECK (run_type IN ('Regular','OffCycle','Retro','FinalSettlement')),
    calculation_state varchar(20) NOT NULL DEFAULT 'Draft' CHECK (calculation_state IN ('Draft','Captured','Calculating','Calculated','Failed')),
    approval_state varchar(20) NOT NULL DEFAULT 'NotRequired' CHECK (approval_state IN ('NotRequired','Pending','Approved','Rejected')),
    finalization_state varchar(20) NOT NULL DEFAULT 'Open' CHECK (finalization_state IN ('Open','Finalized','Cancelled')),
    payment_state varchar(20) NOT NULL DEFAULT 'NotReady' CHECK (payment_state IN ('NotReady','Ready','Requested','Partial','Paid','Failed')),
    accounting_state varchar(20) NOT NULL DEFAULT 'NotReady' CHECK (accounting_state IN ('NotReady','Ready','Posted','Failed')),
    currency_code char(3) NOT NULL, total_gross numeric(19,4) NOT NULL DEFAULT 0, total_deductions numeric(19,4) NOT NULL DEFAULT 0,
    total_net numeric(19,4) NOT NULL DEFAULT 0, employee_count integer NOT NULL DEFAULT 0, created_at timestamptz NOT NULL, updated_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,payroll_group_id,period_id,run_type)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_run_employee (
    tenant_id uuid NOT NULL, id uuid NOT NULL, run_id uuid NOT NULL, employment_id uuid NOT NULL, hr_snapshot_id uuid NULL,
    calculation_hash char(64) NULL, status varchar(20) NOT NULL DEFAULT 'Pending' CHECK (status IN ('Pending','Calculated','Exception','Finalized')),
    gross numeric(19,4) NOT NULL DEFAULT 0, deductions numeric(19,4) NOT NULL DEFAULT 0, net numeric(19,4) NOT NULL DEFAULT 0,
    employer_cost numeric(19,4) NOT NULL DEFAULT 0, exception_code varchar(80) NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,run_id,employment_id)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_calculation_line (
    tenant_id uuid NOT NULL, id uuid NOT NULL, run_employee_id uuid NOT NULL, component_id uuid NOT NULL, component_version_id uuid NULL,
    quantity numeric(19,6) NOT NULL DEFAULT 1, rate numeric(19,6) NULL, amount numeric(19,4) NOT NULL, source varchar(30) NOT NULL,
    trace jsonb NOT NULL DEFAULT '{}'::jsonb, PRIMARY KEY (tenant_id,id)
);
CREATE TABLE IF NOT EXISTS business_payroll.payslip (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, run_employee_id uuid NOT NULL, payslip_number varchar(60) NOT NULL,
    employment_id uuid NOT NULL, gross numeric(19,4) NOT NULL, deductions numeric(19,4) NOT NULL, net numeric(19,4) NOT NULL,
    employer_cost numeric(19,4) NOT NULL, status varchar(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft','Finalized','Corrected','Reversed')),
    finalized_at timestamptz NULL, calculation_hash char(64) NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,run_employee_id), UNIQUE (tenant_id,payslip_number)
);
CREATE TABLE IF NOT EXISTS business_payroll.payslip_line (
    tenant_id uuid NOT NULL, id uuid NOT NULL, payslip_id uuid NOT NULL, component_id uuid NOT NULL, code varchar(50) NOT NULL,
    name varchar(160) NOT NULL, category varchar(30) NOT NULL, amount numeric(19,4) NOT NULL, trace jsonb NOT NULL DEFAULT '{}'::jsonb,
    PRIMARY KEY (tenant_id,id)
);
CREATE TABLE IF NOT EXISTS business_payroll.employee_receivable (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, employment_id uuid NOT NULL, type varchar(20) NOT NULL CHECK (type IN ('Advance','Loan')),
    principal numeric(19,4) NOT NULL CHECK (principal > 0), outstanding_principal numeric(19,4) NOT NULL CHECK (outstanding_principal >= 0),
    currency_code char(3) NOT NULL, status varchar(20) NOT NULL DEFAULT 'Draft' CHECK (status IN ('Draft','Approved','Disbursed','Paused','Settled','WrittenOff','Cancelled')),
    installment_amount numeric(19,4) NULL CHECK (installment_amount IS NULL OR installment_amount > 0), installment_count integer NULL CHECK (installment_count IS NULL OR installment_count > 0),
    created_at timestamptz NOT NULL, approved_at timestamptz NULL, disbursed_at timestamptz NULL, PRIMARY KEY (tenant_id,id)
);
CREATE TABLE IF NOT EXISTS business_payroll.recovery_installment (
    tenant_id uuid NOT NULL, id uuid NOT NULL, receivable_id uuid NOT NULL, installment_number integer NOT NULL CHECK (installment_number > 0), due_date date NOT NULL,
    amount numeric(19,4) NOT NULL CHECK (amount > 0), applied_amount numeric(19,4) NOT NULL DEFAULT 0 CHECK (applied_amount >= 0), status varchar(20) NOT NULL DEFAULT 'Scheduled' CHECK (status IN ('Scheduled','Applied','Paused','Cancelled')),
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,receivable_id,installment_number), CHECK (applied_amount <= amount)
);
CREATE TABLE IF NOT EXISTS business_payroll.payment_instruction (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, run_id uuid NOT NULL, employment_id uuid NOT NULL,
    amount numeric(19,4) NOT NULL CHECK (amount >= 0), currency_code char(3) NOT NULL, destination_ref varchar(200) NULL,
    status varchar(20) NOT NULL DEFAULT 'Prepared' CHECK (status IN ('Prepared','Requested','Partial','Paid','Failed','Cancelled')),
    external_reference varchar(200) NULL, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,run_id,employment_id)
);
CREATE TABLE IF NOT EXISTS business_payroll.accounting_fact (
    tenant_id uuid NOT NULL, legal_entity_id uuid NOT NULL, id uuid NOT NULL, run_id uuid NOT NULL, fact_type varchar(40) NOT NULL,
    total_amount numeric(19,4) NOT NULL, currency_code char(3) NOT NULL, status varchar(20) NOT NULL DEFAULT 'Ready' CHECK (status IN ('Ready','Posted','Failed')),
    idempotency_key varchar(200) NOT NULL, payload jsonb NOT NULL DEFAULT '{}'::jsonb, created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,idempotency_key)
);

CREATE INDEX IF NOT EXISTS ix_payroll_period_group_dates ON business_payroll.payroll_period(tenant_id,payroll_group_id,period_start DESC);
CREATE INDEX IF NOT EXISTS ix_payroll_assignment_employment ON business_payroll.employee_payroll_assignment(tenant_id,employment_id,effective_from DESC);
CREATE INDEX IF NOT EXISTS ix_payroll_input_cutoff ON business_payroll.payroll_input(tenant_id,legal_entity_id,status,input_date);
CREATE INDEX IF NOT EXISTS ix_payroll_run_state ON business_payroll.payroll_run(tenant_id,legal_entity_id,finalization_state,created_at DESC);
CREATE INDEX IF NOT EXISTS ix_payroll_payslip_employment ON business_payroll.payslip(tenant_id,employment_id,finalized_at DESC);
CREATE INDEX IF NOT EXISTS ix_payroll_receivable_employment ON business_payroll.employee_receivable(tenant_id,employment_id,status);

CREATE OR REPLACE FUNCTION business_payroll.enforce_assignment_overlap() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF EXISTS (SELECT 1 FROM business_payroll.employee_payroll_assignment a WHERE a.tenant_id=NEW.tenant_id AND a.employment_id=NEW.employment_id AND a.id<>NEW.id AND daterange(a.effective_from, a.effective_to, '[)') && daterange(NEW.effective_from, NEW.effective_to, '[)')) THEN
        RAISE EXCEPTION 'payroll.assignment_overlap' USING ERRCODE='23P01';
    END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS trg_assignment_overlap ON business_payroll.employee_payroll_assignment;
CREATE TRIGGER trg_assignment_overlap BEFORE INSERT OR UPDATE ON business_payroll.employee_payroll_assignment FOR EACH ROW EXECUTE FUNCTION business_payroll.enforce_assignment_overlap();

DO $$ DECLARE item text; BEGIN
  FOREACH item IN ARRAY ARRAY['payroll_group','payroll_period','payroll_component','payroll_component_version','salary_structure','salary_structure_version','salary_structure_component','employee_payroll_assignment','payroll_input','payroll_run','payroll_run_employee','payroll_calculation_line','payslip','payslip_line','employee_receivable','recovery_installment','payment_instruction','accounting_fact'] LOOP
    EXECUTE format('ALTER TABLE business_payroll.%I ENABLE ROW LEVEL SECURITY', item);
    EXECUTE format('ALTER TABLE business_payroll.%I FORCE ROW LEVEL SECURITY', item);
    EXECUTE format('DROP POLICY IF EXISTS tenant_isolation ON business_payroll.%I', item);
    EXECUTE format('CREATE POLICY tenant_isolation ON business_payroll.%I USING (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting(''app.tenant_id'', true), '''')::uuid)', item);
  END LOOP;
END $$;
