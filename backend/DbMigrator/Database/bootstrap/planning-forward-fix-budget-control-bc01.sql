-- BC01: preserve control-currency evidence for canonical Actual and correction facts.
alter table business_planning.budget_consumption_fact
  add column if not exists source_currency char(3);

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'budget_consumption_source_currency_check') then
    alter table business_planning.budget_consumption_fact
      add constraint budget_consumption_source_currency_check check (source_currency is null or length(btrim(source_currency)) = 3);
  end if;
end $$;

create index if not exists ix_budget_consumption_correction
  on business_planning.budget_consumption_fact(tenant_id,correction_of_id,state,created_at);

alter table business_planning.budget_override
  add column if not exists source_module text,
  add column if not exists source_document_id text,
  add column if not exists source_line_id text,
  add column if not exists source_version text,
  add column if not exists currency_code char(3),
  add column if not exists used_amount numeric(19,4) not null default 0,
  add column if not exists valid_until timestamptz,
  add column if not exists approved_at timestamptz,
  add column if not exists approval_request_id uuid;

do $$
begin
  alter table business_planning.budget_override drop constraint if exists budget_override_state_check;
  if not exists (select 1 from pg_constraint where conname = 'budget_override_state_check') then
    alter table business_planning.budget_override
      add constraint budget_override_state_check check (state in ('Requested','Pending','Approved','Rejected'));
  end if;
end $$;

alter table business_planning.budget_control_policy
  add column if not exists control_horizon text not null default 'Monthly',
  add column if not exists carryforward_mode text not null default 'None',
  add column if not exists negative_budget_allowed boolean not null default false,
  add column if not exists shadow_mode boolean not null default false,
  add column if not exists opening_reconciled boolean not null default false,
  add column if not exists strict_activation_at timestamptz;

do $$
begin
  alter table business_planning.budget_control_policy drop constraint if exists budget_control_policy_basis_check;
  if not exists (select 1 from pg_constraint where conname = 'budget_control_policy_basis_check') then
    alter table business_planning.budget_control_policy
      add constraint budget_control_policy_basis_check check (basis in ('Precommitment','Commitment','CommitmentAndActual','Actual','ActualAndCommitment','ActualPrecommitmentCommitment','ActualOpenCommitmentProposed'));
  end if;
end $$;

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'budget_control_policy_horizon_check') then
    alter table business_planning.budget_control_policy
      add constraint budget_control_policy_horizon_check check (control_horizon in ('Monthly','Quarterly','Annual'));
  end if;
  if not exists (select 1 from pg_constraint where conname = 'budget_control_policy_carryforward_check') then
    alter table business_planning.budget_control_policy
      add constraint budget_control_policy_carryforward_check check (carryforward_mode in ('None','WithinFiscalYear','ExplicitTransfer','YearEndCarryforward'));
  end if;
end $$;

do $$
begin
  if not exists (select 1 from pg_constraint where conname = 'budget_override_used_amount_check') then
    alter table business_planning.budget_override
      add constraint budget_override_used_amount_check check (used_amount >= 0 and used_amount <= amount);
  end if;
end $$;

create unique index if not exists ux_budget_override_source
  on business_planning.budget_override(tenant_id,source_module,source_document_id,source_line_id,source_version)
  where source_module is not null and source_document_id is not null and source_line_id is not null and source_version is not null;

create table if not exists business_planning.budget_control_reconciliation_run (
  tenant_id uuid not null,
  id uuid not null,
  bucket_id uuid,
  state text not null,
  rebuild_applied boolean not null default false,
  bucket_count integer not null default 0,
  discrepancy_amount numeric(19,4) not null default 0,
  started_at timestamptz not null default now(),
  completed_at timestamptz,
  error_detail text,
  requested_by uuid not null,
  primary key (tenant_id,id),
  foreign key (tenant_id,bucket_id) references business_planning.budget_control_bucket(tenant_id,id),
  check (state in ('Running','Succeeded','Failed')),
  check (bucket_count >= 0),
  check (discrepancy_amount >= 0)
);
create index if not exists ix_budget_control_reconciliation_runs on business_planning.budget_control_reconciliation_run(tenant_id,started_at desc);
alter table business_planning.budget_control_reconciliation_run enable row level security;
alter table business_planning.budget_control_reconciliation_run force row level security;
drop policy if exists tenant_isolation on business_planning.budget_control_reconciliation_run;
create policy tenant_isolation on business_planning.budget_control_reconciliation_run
  using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
  with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update,delete on business_planning.budget_control_reconciliation_run to erp_app;
