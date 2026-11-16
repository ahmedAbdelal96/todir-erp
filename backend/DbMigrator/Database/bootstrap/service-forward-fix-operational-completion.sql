-- Service operational completion: rounds, recovery, and fixed-asset history projection.
-- Service stores execution evidence and references only; owning modules keep their masters.
create table if not exists business_service.maintenance_round (
  tenant_id uuid not null,
  id uuid not null,
  legal_entity_id uuid not null,
  code text not null,
  name text not null,
  scheduled_date date not null,
  state text not null default 'Draft',
  row_version bigint not null default 1,
  created_by uuid not null,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,legal_entity_id,code),
  foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
  check (state in ('Draft','Planned','InProgress','Completed','Cancelled')),
  check (row_version > 0)
);
create table if not exists business_service.maintenance_round_subject (
  tenant_id uuid not null,
  id uuid not null,
  round_id uuid not null,
  subject_type text not null,
  subject_id uuid not null,
  sequence_no integer not null,
  state text not null default 'Open',
  result text,
  measurements jsonb not null default '{}'::jsonb,
  exceptions text,
  follow_up text,
  recorded_by uuid,
  recorded_at timestamptz,
  primary key (tenant_id,id),
  unique (tenant_id,round_id,subject_type,subject_id),
  foreign key (tenant_id,round_id) references business_service.maintenance_round(tenant_id,id),
  check (subject_type in ('InstalledBase','FixedAsset')),
  check (state in ('Open','Passed','Failed','Skipped')),
  check (jsonb_typeof(measurements) = 'object')
);
create table if not exists business_service.service_supplier_recovery (
  tenant_id uuid not null,
  id uuid not null,
  work_order_id uuid not null,
  supplier_party_id uuid not null,
  source_reference text not null,
  amount numeric(19,4) not null,
  currency_code char(3) not null,
  state text not null default 'Draft',
  reason text,
  idempotency_key text not null,
  created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,idempotency_key),
  foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id),
  check (amount > 0),
  check (state in ('Draft','Requested','Recovered','Rejected','Cancelled'))
);
create table if not exists business_service.service_fixed_asset_history (
  tenant_id uuid not null,
  id uuid not null,
  fixed_asset_id uuid not null,
  work_order_id uuid not null,
  event_type text not null,
  cost_amount numeric(19,4) not null default 0,
  currency_code char(3) not null default 'EGP',
  snapshot jsonb not null default '{}'::jsonb,
  occurred_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,fixed_asset_id,work_order_id,event_type),
  foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id),
  check (event_type in ('TechnicalCompleted','Accepted','Closed','CapitalImprovementCandidate')),
  check (cost_amount >= 0),
  check (jsonb_typeof(snapshot) = 'object')
);

create index if not exists ix_service_round_schedule on business_service.maintenance_round(tenant_id,scheduled_date,state,id);
create index if not exists ix_service_round_subject on business_service.maintenance_round_subject(tenant_id,round_id,sequence_no);
create index if not exists ix_service_supplier_recovery on business_service.service_supplier_recovery(tenant_id,work_order_id,state,id);
create index if not exists ix_service_fixed_asset_history on business_service.service_fixed_asset_history(tenant_id,fixed_asset_id,occurred_at desc,id);

do $$ declare t text; begin
  foreach t in array array['maintenance_round','maintenance_round_subject','service_supplier_recovery','service_fixed_asset_history'] loop
    execute format('alter table business_service.%I enable row level security', t);
    execute format('alter table business_service.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_service.%I', t);
    execute format('create policy tenant_isolation on business_service.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_service.%I to erp_app', t);
  end loop;
end $$;
