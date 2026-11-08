create table if not exists business_expenses.expense_card_allocation (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, card_transaction_id uuid not null, expense_id uuid not null,
 amount numeric(19,4) not null, created_at timestamptz not null default now(), primary key (tenant_id,id),
 unique (tenant_id,card_transaction_id,expense_id),
 foreign key (tenant_id,card_transaction_id) references business_expenses.expense_card_transaction(tenant_id,id),
 foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check (amount > 0)
);
alter table business_expenses.expense_card_allocation enable row level security;
alter table business_expenses.expense_card_allocation force row level security;
drop policy if exists expense_card_allocation_tenant_isolation on business_expenses.expense_card_allocation;
create policy expense_card_allocation_tenant_isolation on business_expenses.expense_card_allocation using (tenant_id = current_setting('app.tenant_id', true)::uuid) with check (tenant_id = current_setting('app.tenant_id', true)::uuid);
grant select,insert,update,delete on business_expenses.expense_card_allocation to erp_app;
create index if not exists ix_expenses_card_allocations_scope on business_expenses.expense_card_allocation(tenant_id,legal_entity_id,card_transaction_id);
