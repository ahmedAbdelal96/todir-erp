CREATE TABLE IF NOT EXISTS business_payroll.payroll_hr_snapshot (
    tenant_id uuid NOT NULL, id uuid NOT NULL, legal_entity_id uuid NOT NULL, payroll_run_id uuid NOT NULL,
    contract_version integer NOT NULL, period_start date NOT NULL, period_end date NOT NULL, cutoff_at timestamptz NOT NULL,
    payload_sha256 char(64) NOT NULL, payload jsonb NOT NULL, created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,payroll_run_id), UNIQUE (tenant_id,payroll_run_id,payload_sha256)
);
ALTER TABLE business_payroll.payroll_run ADD COLUMN IF NOT EXISTS hr_snapshot_id uuid NULL;
ALTER TABLE business_payroll.payroll_run_employee ADD COLUMN IF NOT EXISTS snapshot_employment_number varchar(80) NULL;
ALTER TABLE business_payroll.payroll_run_employee ADD COLUMN IF NOT EXISTS calculated_at timestamptz NULL;
CREATE TABLE IF NOT EXISTS business_payroll.payroll_exception (
    tenant_id uuid NOT NULL, id uuid NOT NULL, run_id uuid NOT NULL, employment_id uuid NULL, component_id uuid NULL,
    code varchar(100) NOT NULL, severity varchar(20) NOT NULL CHECK (severity IN ('Info','Warning','Blocking')),
    explanation varchar(1000) NOT NULL, required_action varchar(1000) NOT NULL, is_resolved boolean NOT NULL DEFAULT false,
    created_at timestamptz NOT NULL, resolved_at timestamptz NULL, PRIMARY KEY (tenant_id,id), UNIQUE (tenant_id,run_id,employment_id,code)
);
CREATE TABLE IF NOT EXISTS business_payroll.payroll_variance (
    tenant_id uuid NOT NULL, id uuid NOT NULL, run_id uuid NOT NULL, employment_id uuid NOT NULL, component_id uuid NULL,
    prior_amount numeric(19,4) NOT NULL, current_amount numeric(19,4) NOT NULL, variance_amount numeric(19,4) NOT NULL,
    variance_percent numeric(12,6) NULL, severity varchar(20) NOT NULL CHECK (severity IN ('Info','Warning','Blocking')), explanation varchar(1000) NOT NULL,
    created_at timestamptz NOT NULL, PRIMARY KEY (tenant_id,id)
);
ALTER TABLE business_payroll.payroll_hr_snapshot ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.payroll_hr_snapshot FORCE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.payroll_exception ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.payroll_exception FORCE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.payroll_variance ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.payroll_variance FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_payroll.payroll_hr_snapshot;
DROP POLICY IF EXISTS tenant_isolation ON business_payroll.payroll_exception;
DROP POLICY IF EXISTS tenant_isolation ON business_payroll.payroll_variance;
CREATE POLICY tenant_isolation ON business_payroll.payroll_hr_snapshot USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_isolation ON business_payroll.payroll_exception USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
CREATE POLICY tenant_isolation ON business_payroll.payroll_variance USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid) WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
CREATE INDEX IF NOT EXISTS ix_payroll_snapshot_run ON business_payroll.payroll_hr_snapshot(tenant_id,payroll_run_id);
CREATE INDEX IF NOT EXISTS ix_payroll_exception_run ON business_payroll.payroll_exception(tenant_id,run_id,is_resolved,severity);
CREATE INDEX IF NOT EXISTS ix_payroll_variance_run ON business_payroll.payroll_variance(tenant_id,run_id,employment_id);

CREATE OR REPLACE FUNCTION business_payroll.prevent_finalized_payslip_mutation() RETURNS trigger LANGUAGE plpgsql AS $$
BEGIN
    IF OLD.status = 'Finalized' THEN RAISE EXCEPTION 'payroll.payslip_immutable' USING ERRCODE='55000'; END IF;
    RETURN NEW;
END $$;
DROP TRIGGER IF EXISTS trg_payslip_immutable ON business_payroll.payslip;
CREATE TRIGGER trg_payslip_immutable BEFORE UPDATE OR DELETE ON business_payroll.payslip FOR EACH ROW EXECUTE FUNCTION business_payroll.prevent_finalized_payslip_mutation();

DO $$ BEGIN
  IF NOT EXISTS (SELECT 1 FROM pg_trigger WHERE tgname='trg_payslip_line_immutable') THEN
    CREATE OR REPLACE FUNCTION business_payroll.prevent_finalized_payslip_line_mutation() RETURNS trigger LANGUAGE plpgsql AS $fn$
    BEGIN
      IF EXISTS (SELECT 1 FROM business_payroll.payslip p WHERE p.tenant_id=OLD.tenant_id AND p.id=OLD.payslip_id AND p.status='Finalized') THEN RAISE EXCEPTION 'payroll.payslip_immutable' USING ERRCODE='55000'; END IF;
      RETURN COALESCE(NEW, OLD);
    END $fn$;
    CREATE TRIGGER trg_payslip_line_immutable BEFORE UPDATE OR DELETE ON business_payroll.payslip_line FOR EACH ROW EXECUTE FUNCTION business_payroll.prevent_finalized_payslip_line_mutation();
  END IF;
END $$;
