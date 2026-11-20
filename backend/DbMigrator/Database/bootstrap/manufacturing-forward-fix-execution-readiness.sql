-- Forward-only Manufacturing execution/readiness hardening.
-- This migration adds durable shop-floor events and close evidence without
-- changing any applied baseline or taking ownership of Inventory/Quality facts.

alter table business_manufacturing.production_operation
  add column if not exists row_version bigint not null default 1,
  add column if not exists started_at timestamptz,
  add column if not exists paused_at timestamptz,
  add column if not exists completed_at timestamptz,
  add column if not exists started_by uuid,
  add column if not exists actual_good_qty numeric(28,10) not null default 0,
  add column if not exists actual_scrap_qty numeric(28,10) not null default 0;

alter table business_manufacturing.planned_supply
  add column if not exists firming_key text;

create unique index if not exists ux_mfg_planned_supply_firming_key
  on business_manufacturing.planned_supply(tenant_id,firming_key)
  where firming_key is not null;

create table if not exists business_manufacturing.production_operation_event (
  tenant_id uuid not null,
  id uuid not null,
  production_operation_id uuid not null,
  event_type text not null,
  quantity numeric(28,10),
  actual_minutes numeric(18,4),
  reason text,
  idempotency_key text not null,
  actor_id uuid not null,
  occurred_at timestamptz not null default now(),
  payload jsonb not null default '{}'::jsonb,
  primary key (tenant_id,id),
  unique (tenant_id,production_operation_id,idempotency_key),
  foreign key (tenant_id,production_operation_id) references business_manufacturing.production_operation(tenant_id,id),
  check (event_type in ('Started','Paused','Resumed','Progressed','Completed','Blocked')),
  check (quantity is null or quantity > 0),
  check (actual_minutes is null or actual_minutes >= 0)
);

create table if not exists business_manufacturing.production_close_reconciliation (
  tenant_id uuid not null,
  id uuid not null,
  production_order_id uuid not null,
  material_issued_qty numeric(28,10) not null,
  material_accounted_qty numeric(28,10) not null,
  good_output_qty numeric(28,10) not null,
  scrap_qty numeric(28,10) not null,
  operation_completed boolean not null,
  quality_state text not null,
  cost_state text not null,
  wip_amount numeric(28,10) not null default 0,
  state text not null default 'Ready',
  idempotency_key text not null,
  created_at timestamptz not null default now(),
  closed_at timestamptz,
  primary key (tenant_id,id),
  unique (tenant_id,production_order_id),
  unique (tenant_id,idempotency_key),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  check (material_issued_qty >= 0 and material_accounted_qty >= 0 and good_output_qty >= 0 and scrap_qty >= 0),
  check (state in ('Ready','Blocked','Closed','Reversed'))
);

create index if not exists ix_mfg_operation_events on business_manufacturing.production_operation_event(tenant_id,production_operation_id,occurred_at,id);
create index if not exists ix_mfg_close_reconciliation on business_manufacturing.production_close_reconciliation(tenant_id,state,created_at desc);

revoke all on table business_manufacturing.production_operation_event from public;
revoke all on table business_manufacturing.production_close_reconciliation from public;
grant select,insert,update,delete on table business_manufacturing.production_operation_event to erp_app;
grant select,insert,update,delete on table business_manufacturing.production_close_reconciliation to erp_app;

do $$
declare table_name text;
begin
  foreach table_name in array array['production_operation_event','production_close_reconciliation'] loop
    execute format('alter table business_manufacturing.%I enable row level security', table_name);
    execute format('alter table business_manufacturing.%I force row level security', table_name);
    execute format('drop policy if exists tenant_isolation on business_manufacturing.%I', table_name);
    execute format('create policy tenant_isolation on business_manufacturing.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
  end loop;
end $$;
