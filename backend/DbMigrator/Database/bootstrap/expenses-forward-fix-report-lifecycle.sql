create table if not exists business_expenses.expense_report_history (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, report_id uuid not null,
 action text not null, to_state jsonb not null default '{}'::jsonb, occurred_at timestamptz not null default now(),
 primary key (tenant_id,id), foreign key (tenant_id,report_id) references business_expenses.expense_report(tenant_id,id),
 check (action in ('submit','approve','reject','finalize'))
);
alter table business_expenses.expense_report_history enable row level security;
alter table business_expenses.expense_report_history force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_report_history;
create policy tenant_isolation on business_expenses.expense_report_history using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert on business_expenses.expense_report_history to erp_app;
create index if not exists ix_expenses_report_history_scope on business_expenses.expense_report_history(tenant_id,legal_entity_id,report_id,occurred_at,id);
