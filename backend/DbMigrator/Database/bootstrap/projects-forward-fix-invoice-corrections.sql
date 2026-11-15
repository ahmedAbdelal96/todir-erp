-- Preserve the immutable invoice allocation history while projecting finalized credit/debit corrections.
create table if not exists business_projects.project_invoice_correction (
  tenant_id uuid not null,
  id uuid not null,
  project_id uuid not null,
  correction_invoice_id uuid not null,
  original_invoice_id uuid not null,
  amount numeric(19,4) not null,
  currency_code char(3) not null,
  state text not null default 'Applied',
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,correction_invoice_id),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (amount > 0),
  check (length(currency_code)=3),
  check (state in ('Applied','Ignored'))
);

create index if not exists ix_projects_invoice_correction on business_projects.project_invoice_correction(tenant_id,project_id,created_at desc,id desc);
create index if not exists ix_projects_milestones_page on business_projects.project_milestone(tenant_id,project_id,due_date,id);
create index if not exists ix_projects_team_page on business_projects.project_team_member(tenant_id,project_id,active_from,id);

alter table business_projects.project_invoice_correction enable row level security;
alter table business_projects.project_invoice_correction force row level security;
drop policy if exists tenant_isolation on business_projects.project_invoice_correction;
create policy tenant_isolation on business_projects.project_invoice_correction
  using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
  with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update,delete on business_projects.project_invoice_correction to erp_app;
