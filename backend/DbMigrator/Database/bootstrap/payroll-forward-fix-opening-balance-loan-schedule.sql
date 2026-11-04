alter table business_payroll.payroll_opening_balance
    add column if not exists installment_amount numeric(19,4) null,
    add column if not exists installment_count integer null;

alter table business_payroll.payroll_opening_balance
    drop constraint if exists payroll_opening_balance_loan_schedule_check;

alter table business_payroll.payroll_opening_balance
    add constraint payroll_opening_balance_loan_schedule_check
    check ((balance_type in ('Loan','Advance') and installment_amount is not null and installment_amount > 0 and installment_count is not null and installment_count > 0 and installment_amount <= amount)
        or balance_type not in ('Loan','Advance')) not valid;
