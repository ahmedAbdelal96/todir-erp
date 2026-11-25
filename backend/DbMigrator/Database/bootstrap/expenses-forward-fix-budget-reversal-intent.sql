-- BC01: make Expense reversal recovery explicit across the synchronous Ledger boundary.
create table if not exists business_expenses.expense_budget_reversal_intent (
  tenant_id uuid not null,
  legal_entity_id uuid not null,
  expense_id uuid not null,
  ledger_entry_id uuid not null,
  reversal_entry_id uuid,
  source_version bigint not null,
  amount numeric(19,4) not null,
  base_amount numeric(19,4) not null,
  currency_code char(3) not null,
  reason text not null,
  state text not null default 'PendingLedger',
  attempt_count integer not null default 0,
  last_error text,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (tenant_id,expense_id),
  foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id),
  check (amount > 0 and base_amount > 0 and source_version > 0),
  check (length(currency_code)=3),
  check (state in ('PendingLedger','PendingCorrection','Completed','Failed'))
);
alter table business_expenses.expense_budget_reversal_intent enable row level security;
alter table business_expenses.expense_budget_reversal_intent force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_budget_reversal_intent;
create policy tenant_isolation on business_expenses.expense_budget_reversal_intent
  using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
  with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
create index if not exists ix_expense_budget_reversal_intent_state on business_expenses.expense_budget_reversal_intent(tenant_id,state,updated_at);
grant select,insert,update on business_expenses.expense_budget_reversal_intent to erp_app;
