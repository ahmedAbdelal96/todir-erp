CREATE TABLE IF NOT EXISTS business_payroll.recovery_application (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    receivable_id uuid NOT NULL,
    installment_id uuid NOT NULL,
    run_id uuid NOT NULL,
    run_employee_id uuid NOT NULL,
    amount numeric(19,4) NOT NULL CHECK (amount > 0),
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,installment_id,run_id),
    CHECK (amount > 0)
);

CREATE INDEX IF NOT EXISTS ix_payroll_recovery_application_run
    ON business_payroll.recovery_application(tenant_id,legal_entity_id,run_id,created_at);

ALTER TABLE business_payroll.recovery_application ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.recovery_application FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_payroll.recovery_application;
CREATE POLICY tenant_isolation ON business_payroll.recovery_application
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);

GRANT SELECT, INSERT, UPDATE ON business_payroll.recovery_application TO erp_app;
