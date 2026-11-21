-- Forward-only planning, resource, quality and financial evidence extensions.
-- Manufacturing owns the planning snapshot and execution attribution; source
-- modules remain owners of inventory, maintenance, HR, quality and ledger facts.

-- Foreign-key validation reads the referenced tables. Temporarily remove FORCE
-- RLS from the existing same-schema parents while adding these constraints;
-- the tenant policies remain enabled and are restored at the end.
alter table business_manufacturing.planning_demand no force row level security;
alter table business_manufacturing.planned_supply no force row level security;
alter table business_manufacturing.production_order no force row level security;
alter table business_manufacturing.production_operation no force row level security;
alter table business_manufacturing.manufacturing_resource no force row level security;
alter table business_manufacturing.production_output no force row level security;

alter table business_manufacturing.manufacturing_product_profile
  add column if not exists purchase_lead_time_days integer not null default 0,
  add column if not exists manufacturing_lead_time_days integer not null default 0,
  add column if not exists safety_stock_qty numeric(28,10) not null default 0,
  add column if not exists minimum_order_qty numeric(28,10) not null default 0,
  add column if not exists order_multiple numeric(28,10) not null default 1,
  add column if not exists maximum_order_qty numeric(28,10),
  add column if not exists lot_sizing_policy text not null default 'ExactRequirement',
  add column if not exists firming_fence_days integer not null default 0,
  add constraint manufacturing_profile_planning_policy_check check (purchase_lead_time_days >= 0 and manufacturing_lead_time_days >= 0 and safety_stock_qty >= 0 and minimum_order_qty >= 0 and order_multiple > 0 and (maximum_order_qty is null or maximum_order_qty >= minimum_order_qty) and firming_fence_days >= 0),
  add constraint manufacturing_profile_lot_policy_check check (lot_sizing_policy in ('ExactRequirement','FixedLot','MinMax','PeriodLot'));

create table if not exists business_manufacturing.plant_warehouse_scope (
  tenant_id uuid not null,
  id uuid not null,
  plant_id uuid not null,
  warehouse_id uuid not null,
  active boolean not null default true,
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,plant_id,warehouse_id),
  foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id)
);

alter table business_manufacturing.planning_demand
  add column if not exists parent_demand_id uuid,
  add column if not exists demand_level integer not null default 0,
  add column if not exists source_version text,
  add constraint planning_demand_level_check check (demand_level >= 0),
  add constraint planning_demand_parent_fk foreign key (tenant_id,parent_demand_id) references business_manufacturing.planning_demand(tenant_id,id);

alter table business_manufacturing.planned_supply
  add column if not exists parent_demand_id uuid,
  add column if not exists source_supply_date date,
  add column if not exists lead_time_days integer not null default 0,
  add column if not exists source_policy text,
  add constraint planned_supply_lead_time_check check (lead_time_days >= 0),
  add constraint planned_supply_parent_demand_fk foreign key (tenant_id,parent_demand_id) references business_manufacturing.planning_demand(tenant_id,id);

create table if not exists business_manufacturing.production_operation_resource (
  tenant_id uuid not null,
  id uuid not null,
  production_operation_id uuid not null,
  resource_id uuid not null,
  required_quantity numeric(18,6) not null default 1,
  assigned_quantity numeric(18,6) not null default 0,
  assigned_at timestamptz,
  released_at timestamptz,
  primary key (tenant_id,id),
  unique (tenant_id,production_operation_id,resource_id),
  foreign key (tenant_id,production_operation_id) references business_manufacturing.production_operation(tenant_id,id),
  foreign key (tenant_id,resource_id) references business_manufacturing.manufacturing_resource(tenant_id,id),
  check (required_quantity > 0 and assigned_quantity >= 0 and assigned_quantity <= required_quantity)
);

create table if not exists business_manufacturing.production_labor_fact (
  tenant_id uuid not null,
  id uuid not null,
  production_order_id uuid not null,
  production_operation_id uuid,
  resource_id uuid,
  employee_id uuid,
  work_date date not null,
  minutes integer not null,
  rate_amount numeric(28,10) not null,
  currency_code char(3) not null,
  source_ref text not null,
  idempotency_key text not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,idempotency_key),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  foreign key (tenant_id,production_operation_id) references business_manufacturing.production_operation(tenant_id,id),
  foreign key (tenant_id,resource_id) references business_manufacturing.manufacturing_resource(tenant_id,id),
  check (minutes > 0 and rate_amount >= 0)
);

create table if not exists business_manufacturing.production_quality_gate (
  tenant_id uuid not null,
  id uuid not null,
  production_order_id uuid not null,
  production_operation_id uuid,
  required boolean not null default true,
  capability_available boolean not null default false,
  decision_state text not null default 'Pending',
  source_ref text,
  decided_at timestamptz,
  primary key (tenant_id,id),
  unique (tenant_id,production_order_id,production_operation_id),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  foreign key (tenant_id,production_operation_id) references business_manufacturing.production_operation(tenant_id,id),
  check (decision_state in ('Pending','Passed','Failed','Blocked'))
);

create table if not exists business_manufacturing.production_subcontract_commitment (
  tenant_id uuid not null,
  id uuid not null,
  production_order_id uuid not null,
  production_operation_id uuid,
  source_module text not null,
  source_document_id text not null,
  supplier_party_id uuid,
  quantity numeric(28,10) not null,
  uom_code text not null,
  expected_date date not null,
  state text not null default 'Planned',
  committed_amount numeric(28,10) not null default 0,
  currency_code char(3),
  idempotency_key text not null,
  primary key (tenant_id,id),
  unique (tenant_id,idempotency_key),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  foreign key (tenant_id,production_operation_id) references business_manufacturing.production_operation(tenant_id,id),
  check (quantity > 0 and committed_amount >= 0),
  check (state in ('Planned','Committed','Received','Cancelled'))
);

create table if not exists business_manufacturing.production_cost_allocation (
  tenant_id uuid not null,
  id uuid not null,
  production_order_id uuid not null,
  output_id uuid not null,
  allocation_basis text not null,
  allocation_value numeric(28,10) not null,
  allocated_amount numeric(28,10) not null,
  currency_code char(3) not null,
  idempotency_key text not null,
  primary key (tenant_id,id),
  unique (tenant_id,idempotency_key),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  foreign key (tenant_id,output_id) references business_manufacturing.production_output(tenant_id,id),
  check (allocation_basis in ('FixedValue','Percentage','Residual') and allocation_value >= 0 and allocated_amount >= 0)
);

create table if not exists business_manufacturing.mps_plan (
  tenant_id uuid not null,
  id uuid not null,
  legal_entity_id uuid not null,
  plant_id uuid not null,
  product_variant_id uuid not null,
  bucket_start date not null,
  bucket_end date not null,
  quantity numeric(28,10) not null,
  uom_code text not null,
  state text not null default 'Draft',
  source_ref text,
  version integer not null default 1,
  created_by uuid not null,
  created_at timestamptz not null default now(),
  primary key (tenant_id,id),
  unique (tenant_id,legal_entity_id,plant_id,product_variant_id,bucket_start,version),
  foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
  foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id),
  check (bucket_end >= bucket_start and quantity > 0 and state in ('Draft','Approved','Superseded','Cancelled'))
);

create index if not exists ix_mfg_plant_warehouse_scope on business_manufacturing.plant_warehouse_scope(tenant_id,plant_id,warehouse_id) where active;
create index if not exists ix_mfg_planning_demand_parent on business_manufacturing.planning_demand(tenant_id,parent_demand_id,demand_level);
create index if not exists ix_mfg_resource_operation on business_manufacturing.production_operation_resource(tenant_id,production_operation_id);
create index if not exists ix_mfg_labor_order on business_manufacturing.production_labor_fact(tenant_id,production_order_id,work_date);
create index if not exists ix_mfg_subcontract_order on business_manufacturing.production_subcontract_commitment(tenant_id,production_order_id,state);

do $$ declare table_name text; begin
  foreach table_name in array array['plant_warehouse_scope','production_operation_resource','production_labor_fact','production_quality_gate','production_subcontract_commitment','production_cost_allocation','mps_plan'] loop
    execute format('alter table business_manufacturing.%I enable row level security', table_name);
    execute format('alter table business_manufacturing.%I force row level security', table_name);
    execute format('drop policy if exists tenant_isolation on business_manufacturing.%I', table_name);
    execute format('create policy tenant_isolation on business_manufacturing.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', table_name);
    execute format('grant select,insert,update,delete on business_manufacturing.%I to erp_app', table_name);
  end loop;
end $$;

alter table business_manufacturing.planning_demand force row level security;
alter table business_manufacturing.planned_supply force row level security;
alter table business_manufacturing.production_order force row level security;
alter table business_manufacturing.production_operation force row level security;
alter table business_manufacturing.manufacturing_resource force row level security;
alter table business_manufacturing.production_output force row level security;
