create table if not exists business_expenses.expense_missing_receipt_declaration (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, expense_id uuid not null,
 reason text not null, status text not null default 'Declared', declared_by uuid not null,
 declared_at timestamptz not null default now(), resolved_at timestamptz,
 primary key (tenant_id,id), unique (tenant_id,expense_id),
 foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id),
 check (status in ('Declared','Resolved','Rejected')), check (length(trim(reason)) >= 5),
 check ((status='Resolved' and resolved_at is not null) or status <> 'Resolved')
);
create index if not exists ix_expenses_missing_receipt_scope on business_expenses.expense_missing_receipt_declaration(tenant_id,legal_entity_id,status,declared_at desc);
alter table business_expenses.expense_missing_receipt_declaration enable row level security;
alter table business_expenses.expense_missing_receipt_declaration force row level security;
drop policy if exists expense_missing_receipt_tenant_isolation on business_expenses.expense_missing_receipt_declaration;
create policy expense_missing_receipt_tenant_isolation on business_expenses.expense_missing_receipt_declaration using (tenant_id = current_setting('app.tenant_id', true)::uuid) with check (tenant_id = current_setting('app.tenant_id', true)::uuid);
grant select,insert,update on business_expenses.expense_missing_receipt_declaration to erp_app;
