-- Financial evidence for expense finalization and recoverable reversal.
alter table business_expenses.expense
    add column if not exists accounting_fact_id uuid,
    add column if not exists ledger_entry_id uuid,
    add column if not exists reversal_entry_id uuid;

alter table business_expenses.expense
    drop constraint if exists expense_accounting_state_check;
alter table business_expenses.expense
    add constraint expense_accounting_state_check
    check (accounting_state in ('NotFinalized','Pending','Posted','ReversalPending','Reversed'));

create unique index if not exists ux_expenses_accounting_fact
    on business_expenses.expense(tenant_id, accounting_fact_id)
    where accounting_fact_id is not null;
create unique index if not exists ux_expenses_ledger_entry
    on business_expenses.expense(tenant_id, ledger_entry_id)
    where ledger_entry_id is not null;
create index if not exists ix_expenses_reversal_pending
    on business_expenses.expense(tenant_id, legal_entity_id, accounting_state)
    where accounting_state = 'ReversalPending';

grant select,insert,update,delete on business_expenses.expense to erp_app;
