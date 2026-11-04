ALTER TABLE business_payroll.payroll_component_version
    ADD COLUMN IF NOT EXISTS statutory_domain varchar(20) NULL;
ALTER TABLE business_payroll.payroll_component_version
    DROP CONSTRAINT IF EXISTS payroll_component_version_statutory_domain_check;
ALTER TABLE business_payroll.payroll_component_version
    ADD CONSTRAINT payroll_component_version_statutory_domain_check
    CHECK (statutory_domain IS NULL OR statutory_domain IN ('Tax','Insurance'));

ALTER TABLE business_payroll.payroll_run_employee
    ADD COLUMN IF NOT EXISTS statutory_settings_version integer NULL,
    ADD COLUMN IF NOT EXISTS statutory_tax_mode varchar(20) NULL,
    ADD COLUMN IF NOT EXISTS statutory_insurance_mode varchar(20) NULL,
    ADD COLUMN IF NOT EXISTS calculation_evidence jsonb NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE business_payroll.payroll_run_employee
    DROP CONSTRAINT IF EXISTS payroll_run_employee_statutory_mode_check;
ALTER TABLE business_payroll.payroll_run_employee
    ADD CONSTRAINT payroll_run_employee_statutory_mode_check
    CHECK ((statutory_tax_mode IS NULL OR statutory_tax_mode IN ('NotManaged','Manual','Automatic'))
       AND (statutory_insurance_mode IS NULL OR statutory_insurance_mode IN ('NotManaged','Manual','Automatic')));

ALTER TABLE business_payroll.payroll_statutory_setting
    ADD COLUMN IF NOT EXISTS manual_input_policy varchar(20) NOT NULL DEFAULT 'Block';
ALTER TABLE business_payroll.payroll_statutory_setting
    DROP CONSTRAINT IF EXISTS payroll_statutory_setting_manual_input_policy_check;
ALTER TABLE business_payroll.payroll_statutory_setting
    ADD CONSTRAINT payroll_statutory_setting_manual_input_policy_check
    CHECK (manual_input_policy IN ('Block','Skip'));
