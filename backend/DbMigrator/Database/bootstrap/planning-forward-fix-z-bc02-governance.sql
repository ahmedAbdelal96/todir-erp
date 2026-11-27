-- BC02: authoritative Actual snapshots, versioned financial mappings and
-- idempotent budget governance.  Planning stores rebuildable snapshots only;
-- Ledger and the owner modules remain the source of financial truth.

create table if not exists business_planning.planning_metric_mapping (
  tenant_id uuid not null,
  id uuid not null,
  mapping_version text not null,
  source_module text not null,
  source_type text not null,
  source_key text not null,
  target_metric_code text not null,
  legal_entity_id uuid,
  effective_from date,
  effective_to date,
  state text not null default 'Draft',
  created_by uuid not null,
  published_at timestamptz,
  primary key (tenant_id,id),
  unique (tenant_id,mapping_version,source_module,source_type,source_key,legal_entity_id),
  check (state in ('Draft','Published','Retired')),
  check (effective_to is null or effective_from is null or effective_from <= effective_to),
  foreign key (tenant_id,target_metric_code) references business_planning.planning_metric(tenant_id,code)
);

alter table business_planning.planning_metric
  add column if not exists financial_role text,
  add column if not exists unit_code text not null default 'Money',
  add column if not exists precision_scale integer not null default 4,
  add column if not exists favorable_direction text not null default 'Higher',
  add column if not exists active boolean not null default true;

do $$
begin
  if not exists (select 1 from pg_constraint where conname='planning_metric_precision_check') then
    alter table business_planning.planning_metric add constraint planning_metric_precision_check check (precision_scale between 0 and 8);
  end if;
  if not exists (select 1 from pg_constraint where conname='planning_metric_direction_check') then
    alter table business_planning.planning_metric add constraint planning_metric_direction_check check (favorable_direction in ('Higher','Lower','Neutral'));
  end if;
end $$;

create table if not exists business_planning.planning_actual_snapshot (
  tenant_id uuid not null,
  id uuid not null,
  plan_version_id uuid not null,
  actual_through date not null,
  mapping_version text not null,
  source_watermark text not null,
  currency_basis char(3) not null,
  source_count integer not null default 0,
  total_amount numeric(19,4) not null default 0,
  reconciliation_state text not null default 'Validated',
  state text not null default 'Staged',
  requested_by uuid not null,
  created_at timestamptz not null default now(),
  published_at timestamptz,
  primary key (tenant_id,id),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id),
  check (source_count >= 0),
  check (reconciliation_state in ('Validated','Failed')),
  check (state in ('Staged','Published','Superseded','Failed'))
);

alter table business_planning.planning_actual_snapshot_fact
  add column if not exists snapshot_id uuid,
  add column if not exists source_line_id text,
  add column if not exists posting_date date,
  add column if not exists fiscal_year integer,
  add column if not exists fiscal_period integer,
  add column if not exists source_kind text not null default 'Actual';

-- PostgreSQL validates a new foreign key with a table-owner scan.  The
-- existing fact table is FORCE RLS, so temporarily remove only FORCE for this
-- metadata validation and restore it before the migration commits.
alter table business_planning.planning_actual_snapshot_fact no force row level security;

alter table business_planning.actual_refresh_run
  add column if not exists plan_version_id uuid,
  add column if not exists actual_through date,
  add column if not exists snapshot_id uuid,
  add column if not exists source_count integer not null default 0,
  add column if not exists reconciliation_state text,
  add column if not exists requested_by uuid;

do $$
begin
  alter table business_planning.planning_actual_snapshot_fact
    drop constraint if exists planning_actual_snapshot_fact_tenant_id_coordinate_id_source_module_source_reference_key;
  if not exists (select 1 from pg_constraint where conname='planning_actual_snapshot_fact_snapshot_fk') then
    alter table business_planning.planning_actual_snapshot_fact
      add constraint planning_actual_snapshot_fact_snapshot_fk
      foreign key (tenant_id,snapshot_id) references business_planning.planning_actual_snapshot(tenant_id,id);
  end if;
  if not exists (select 1 from pg_constraint where conname='planning_actual_snapshot_fact_source_kind_check') then
    alter table business_planning.planning_actual_snapshot_fact
      add constraint planning_actual_snapshot_fact_source_kind_check check (source_kind in ('Actual','Preliminary'));
  end if;
end $$;

create unique index if not exists ux_planning_actual_snapshot_fact_identity
  on business_planning.planning_actual_snapshot_fact(tenant_id,snapshot_id,coordinate_id,source_module,source_reference,coalesce(source_line_id,''))
  where snapshot_id is not null;
create index if not exists ix_planning_actual_snapshot_current
  on business_planning.planning_actual_snapshot(tenant_id,plan_version_id,state,created_at desc);
create index if not exists ix_planning_actual_snapshot_fact_source
  on business_planning.planning_actual_snapshot_fact(tenant_id,snapshot_id,source_module,source_reference);

alter table business_planning.budget_revision add column if not exists idempotency_key text;
alter table business_planning.budget_transfer add column if not exists idempotency_key text;
create unique index if not exists ux_planning_budget_revision_idempotency
  on business_planning.budget_revision(tenant_id,baseline_id,idempotency_key)
  where idempotency_key is not null;
create unique index if not exists ux_planning_budget_transfer_idempotency
  on business_planning.budget_transfer(tenant_id,baseline_id,idempotency_key)
  where idempotency_key is not null;

alter table business_planning.submission_unit
  add column if not exists submitted_by uuid,
  add column if not exists returned_by uuid,
  add column if not exists approved_by uuid,
  add column if not exists approval_request_id uuid;

do $$ declare t text; begin
  foreach t in array array['planning_metric_mapping','planning_actual_snapshot'] loop
    execute format('alter table business_planning.%I enable row level security', t);
    execute format('alter table business_planning.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_planning.%I', t);
    execute format('create policy tenant_isolation on business_planning.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_planning.%I to erp_app', t);
  end loop;
end $$;

alter table business_planning.planning_actual_snapshot_fact enable row level security;
alter table business_planning.planning_actual_snapshot_fact force row level security;
grant select,insert,update,delete on business_planning.planning_actual_snapshot_fact to erp_app;
