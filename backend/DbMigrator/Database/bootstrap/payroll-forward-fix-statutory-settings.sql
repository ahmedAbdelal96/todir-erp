CREATE TABLE IF NOT EXISTS business_payroll.payroll_statutory_setting (
    tenant_id uuid NOT NULL,
    legal_entity_id uuid NOT NULL,
    id uuid NOT NULL,
    tax_mode varchar(20) NOT NULL CHECK (tax_mode IN ('NotManaged','Manual','Automatic')),
    insurance_mode varchar(20) NOT NULL CHECK (insurance_mode IN ('NotManaged','Manual','Automatic')),
    effective_from date NOT NULL,
    version integer NOT NULL CHECK (version > 0),
    actor_id uuid NULL,
    created_at timestamptz NOT NULL,
    PRIMARY KEY (tenant_id,id),
    UNIQUE (tenant_id,legal_entity_id,effective_from),
    UNIQUE (tenant_id,legal_entity_id,version)
);

CREATE INDEX IF NOT EXISTS ix_payroll_statutory_setting_effective
    ON business_payroll.payroll_statutory_setting(tenant_id,legal_entity_id,effective_from DESC);

ALTER TABLE business_payroll.payroll_statutory_setting ENABLE ROW LEVEL SECURITY;
ALTER TABLE business_payroll.payroll_statutory_setting FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS tenant_isolation ON business_payroll.payroll_statutory_setting;
CREATE POLICY tenant_isolation ON business_payroll.payroll_statutory_setting
    USING (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid)
    WITH CHECK (tenant_id = NULLIF(current_setting('app.tenant_id', true), '')::uuid);
