alter table business_payroll.payroll_input
    add column if not exists supplemental_run_id uuid null;

create index if not exists ix_payroll_input_supplemental_target
    on business_payroll.payroll_input(tenant_id, supplemental_run_id, status, consumed_run_id, input_date);
