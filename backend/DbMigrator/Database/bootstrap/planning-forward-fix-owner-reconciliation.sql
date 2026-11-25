-- BC01: durable owner-authoritative opening reconciliation readiness.
create table if not exists business_planning.budget_control_owner_reconciliation (
  tenant_id uuid not null,
  id uuid not null,
  bucket_id uuid not null,
  state text not null,
  source_count integer not null default 0,
  expected_actual numeric(19,4) not null default 0,
  expected_commitment numeric(19,4) not null default 0,
  expected_precommitment numeric(19,4) not null default 0,
  discrepancy_amount numeric(19,4) not null default 0,
  source_watermark text not null,
  completed_at timestamptz,
  error_detail text,
  requested_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,bucket_id),
  foreign key (tenant_id,bucket_id) references business_planning.budget_control_bucket(tenant_id,id),
  check (state in ('Pending','Succeeded','Failed')),
  check (source_count >= 0 and expected_actual >= 0 and expected_commitment >= 0 and expected_precommitment >= 0 and discrepancy_amount >= 0)
);
alter table business_planning.budget_control_owner_reconciliation enable row level security;
alter table business_planning.budget_control_owner_reconciliation force row level security;
drop policy if exists tenant_isolation on business_planning.budget_control_owner_reconciliation;
create policy tenant_isolation on business_planning.budget_control_owner_reconciliation
  using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
  with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update,delete on business_planning.budget_control_owner_reconciliation to erp_app;
