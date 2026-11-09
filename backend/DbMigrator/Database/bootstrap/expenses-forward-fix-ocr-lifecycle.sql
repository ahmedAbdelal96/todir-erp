create table if not exists business_expenses.expense_ocr_job (
 tenant_id uuid not null,
 id uuid not null,
 legal_entity_id uuid not null,
 expense_id uuid not null,
 evidence_id uuid not null,
 state text not null default 'Requested',
 attempt_count integer not null default 0,
 suggestions jsonb,
 failure_code text,
 failure_reason text,
 requested_at timestamptz not null default now(),
 started_at timestamptz,
 completed_at timestamptz,
 primary key (tenant_id,id),
 foreign key (tenant_id,expense_id) references business_expenses.expense(tenant_id,id),
 foreign key (tenant_id,evidence_id) references business_expenses.expense_evidence(tenant_id,id),
 check (state in ('Requested','Processing','Succeeded','Failed','Manual')),
 check (attempt_count >= 0)
);

alter table business_expenses.expense_ocr_job enable row level security;
alter table business_expenses.expense_ocr_job force row level security;
drop policy if exists tenant_isolation on business_expenses.expense_ocr_job;
create policy tenant_isolation on business_expenses.expense_ocr_job
  using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
  with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select, insert, update on business_expenses.expense_ocr_job to erp_app;
create index if not exists ix_expenses_ocr_scope on business_expenses.expense_ocr_job(tenant_id, legal_entity_id, expense_id, requested_at desc);
create unique index if not exists ux_expenses_ocr_active_evidence on business_expenses.expense_ocr_job(tenant_id, evidence_id) where state in ('Requested','Processing','Succeeded','Manual');
