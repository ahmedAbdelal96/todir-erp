create table if not exists business_expenses.expense_import_batch (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, idempotency_key text not null, payload_hash text not null,
 state text not null, total_rows integer not null, created_count integer not null default 0, error_count integer not null default 0,
 created_at timestamptz not null default now(), completed_at timestamptz,
 primary key (tenant_id,id), unique (tenant_id,legal_entity_id,idempotency_key), check (state in ('Processing','Completed','CompletedWithErrors','Failed')), check (total_rows between 1 and 500), check (created_count >= 0 and error_count >= 0 and created_count + error_count <= total_rows)
);
create table if not exists business_expenses.expense_import_row (
 tenant_id uuid not null, batch_id uuid not null, row_number integer not null, external_reference text not null, expense_id uuid, status text not null, error_code text, raw_payload jsonb not null,
 primary key (tenant_id,batch_id,row_number), unique (tenant_id,batch_id,external_reference), foreign key (tenant_id,batch_id) references business_expenses.expense_import_batch(tenant_id,id), foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id), check (status in ('Created','Error'))
);
alter table business_expenses.expense_import_batch enable row level security;
alter table business_expenses.expense_import_batch force row level security;
alter table business_expenses.expense_import_row enable row level security;
alter table business_expenses.expense_import_row force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_import_batch;
drop policy if exists tenant_isolation on business_expenses.expense_import_row;
create policy tenant_isolation on business_expenses.expense_import_batch using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
create policy tenant_isolation on business_expenses.expense_import_row using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid) with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update on business_expenses.expense_import_batch to erp_app;
grant select,insert,update on business_expenses.expense_import_row to erp_app;
create index if not exists ix_expenses_import_batches_scope on business_expenses.expense_import_batch(tenant_id,legal_entity_id,created_at desc,id);
create index if not exists ix_expenses_import_rows_status on business_expenses.expense_import_row(tenant_id,batch_id,status,row_number);
