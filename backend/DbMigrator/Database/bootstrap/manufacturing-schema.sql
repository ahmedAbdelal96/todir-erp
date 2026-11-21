create schema if not exists business_manufacturing authorization erp_owner;
revoke all on schema business_manufacturing from public;
grant usage on schema business_manufacturing to erp_app;

create table if not exists business_manufacturing.plant (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null,
  branch_id uuid, time_zone text not null default 'UTC', state text not null default 'Active', row_version bigint not null default 1,
  created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), foreign key (tenant_id) references platform.tenant(id),
  foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), check (state in ('Active','Inactive')), check (row_version > 0)
);

create table if not exists business_manufacturing.manufacturing_product_profile (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, product_variant_id uuid not null,
  manufacturing_enabled boolean not null default true, default_mode text not null default 'Discrete', make_buy_policy text not null default 'Make',
  default_production_definition_id uuid, default_plant_id uuid, row_version bigint not null default 1, created_by uuid not null,
  created_at timestamptz not null default now(), updated_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,legal_entity_id,product_variant_id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
  check (default_mode in ('Discrete','ProcessBatch')), check (make_buy_policy in ('Make','Buy','MakeOrBuy','Subcontract')), check (row_version > 0)
);

create table if not exists business_manufacturing.bom (
  tenant_id uuid not null, id uuid not null, product_variant_id uuid not null, code text not null, name text not null,
  state text not null default 'Active', created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,code), check (state in ('Active','Archived'))
);
create table if not exists business_manufacturing.bom_version (
  tenant_id uuid not null, id uuid not null, bom_id uuid not null, revision integer not null, state text not null default 'Draft',
  valid_from date not null, valid_to date, base_output_qty numeric(28,10) not null, output_uom_code text not null,
  expected_yield numeric(9,6) not null default 1, expected_scrap numeric(9,6) not null default 0, row_version bigint not null default 1,
  created_by uuid not null, created_at timestamptz not null default now(), approved_by uuid, approved_at timestamptz, activated_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,bom_id,revision), foreign key (tenant_id,bom_id) references business_manufacturing.bom(tenant_id,id),
  check (revision > 0), check (state in ('Draft','UnderReview','Approved','Active','Superseded','Obsolete')),
  check (base_output_qty > 0), check (expected_yield > 0 and expected_yield <= 1), check (expected_scrap >= 0 and expected_scrap < 1),
  check (valid_to is null or valid_to >= valid_from), check (row_version > 0)
);
create table if not exists business_manufacturing.bom_component_line (
  tenant_id uuid not null, id uuid not null, bom_version_id uuid not null, line_no integer not null, component_product_variant_id uuid not null,
  required_qty numeric(28,10) not null, uom_code text not null, scrap_factor numeric(9,6) not null default 0, consumption_method text not null default 'Manual',
  operation_sequence integer, effective_from date, effective_to date, substitute_group_id uuid, phantom boolean not null default false,
  primary key (tenant_id,id), unique (tenant_id,bom_version_id,line_no), foreign key (tenant_id,bom_version_id) references business_manufacturing.bom_version(tenant_id,id),
  check (line_no > 0), check (required_qty > 0), check (scrap_factor >= 0 and scrap_factor < 1), check (consumption_method in ('Manual','Backflush','Either')),
  check (effective_to is null or effective_from is null or effective_to >= effective_from)
);

create table if not exists business_manufacturing.formula (
  tenant_id uuid not null, id uuid not null, product_variant_id uuid not null, code text not null, name text not null, state text not null default 'Active',
  primary key (tenant_id,id), unique (tenant_id,code), check (state in ('Active','Archived'))
);
create table if not exists business_manufacturing.formula_version (
  tenant_id uuid not null, id uuid not null, formula_id uuid not null, revision integer not null, state text not null default 'Draft',
  valid_from date not null, valid_to date, base_batch_qty numeric(28,10) not null, output_uom_code text not null, expected_yield numeric(9,6) not null default 1,
  primary key (tenant_id,id), unique (tenant_id,formula_id,revision), foreign key (tenant_id,formula_id) references business_manufacturing.formula(tenant_id,id),
  check (revision > 0), check (state in ('Draft','Approved','Active','Superseded')), check (base_batch_qty > 0), check (expected_yield > 0 and expected_yield <= 1)
);
create table if not exists business_manufacturing.formula_ingredient_line (
  tenant_id uuid not null, id uuid not null, formula_version_id uuid not null, line_no integer not null, product_variant_id uuid not null,
  quantity numeric(28,10) not null, uom_code text not null, fixed_quantity boolean not null default false, primary key (tenant_id,id),
  unique (tenant_id,formula_version_id,line_no), foreign key (tenant_id,formula_version_id) references business_manufacturing.formula_version(tenant_id,id), check (quantity > 0)
);
create table if not exists business_manufacturing.formula_output_line (
  tenant_id uuid not null, id uuid not null, formula_version_id uuid not null, line_no integer not null, product_variant_id uuid not null,
  output_type text not null, quantity numeric(28,10) not null, uom_code text not null, allocation_basis text not null default 'Residual',
  primary key (tenant_id,id), unique (tenant_id,formula_version_id,line_no), foreign key (tenant_id,formula_version_id) references business_manufacturing.formula_version(tenant_id,id),
  check (output_type in ('Primary','CoProduct','ByProduct','Loss')), check (quantity > 0), check (allocation_basis in ('FixedValue','Percentage','Residual'))
);

create table if not exists business_manufacturing.routing (
  tenant_id uuid not null, id uuid not null, product_variant_id uuid, code text not null, name text not null, state text not null default 'Active',
  primary key (tenant_id,id), unique (tenant_id,code), check (state in ('Active','Archived'))
);
create table if not exists business_manufacturing.routing_version (
  tenant_id uuid not null, id uuid not null, routing_id uuid not null, revision integer not null, state text not null default 'Draft',
  valid_from date not null, valid_to date, primary key (tenant_id,id), unique (tenant_id,routing_id,revision),
  foreign key (tenant_id,routing_id) references business_manufacturing.routing(tenant_id,id), check (revision > 0), check (state in ('Draft','Approved','Active','Superseded')),
  check (valid_to is null or valid_to >= valid_from)
);
create table if not exists business_manufacturing.routing_operation (
  tenant_id uuid not null, id uuid not null, routing_version_id uuid not null, sequence_no integer not null, code text not null, name text not null,
  work_center_id uuid, setup_minutes numeric(18,4) not null default 0, run_minutes_per_unit numeric(18,6) not null default 0,
  queue_minutes numeric(18,4) not null default 0, move_minutes numeric(18,4) not null default 0, operation_type text not null default 'Internal',
  depends_on_sequence integer, quality_gate_required boolean not null default false, primary key (tenant_id,id),
  unique (tenant_id,routing_version_id,sequence_no), foreign key (tenant_id,routing_version_id) references business_manufacturing.routing_version(tenant_id,id),
  check (sequence_no > 0), check (setup_minutes >= 0 and run_minutes_per_unit >= 0 and queue_minutes >= 0 and move_minutes >= 0), check (operation_type in ('Internal','Subcontract'))
);

create table if not exists business_manufacturing.production_definition (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, plant_id uuid not null, product_variant_id uuid not null,
  manufacturing_mode text not null default 'Discrete', bom_version_id uuid, formula_version_id uuid, routing_version_id uuid, valid_from date not null,
  valid_to date, min_qty numeric(28,10), max_qty numeric(28,10), priority integer not null default 100, state text not null default 'Draft', expected_yield numeric(9,6) not null default 1,
  row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id),
  foreign key (tenant_id,bom_version_id) references business_manufacturing.bom_version(tenant_id,id), foreign key (tenant_id,formula_version_id) references business_manufacturing.formula_version(tenant_id,id),
  foreign key (tenant_id,routing_version_id) references business_manufacturing.routing_version(tenant_id,id), check (manufacturing_mode in ('Discrete','ProcessBatch')),
  check ((manufacturing_mode='Discrete' and bom_version_id is not null) or (manufacturing_mode='ProcessBatch' and formula_version_id is not null)), check (valid_to is null or valid_to >= valid_from),
  check (min_qty is null or min_qty > 0), check (max_qty is null or max_qty >= min_qty), check (priority >= 0), check (state in ('Draft','Approved','Active','Superseded','Obsolete')),
  check (expected_yield > 0 and expected_yield <= 1), check (row_version > 0)
);

create table if not exists business_manufacturing.material_substitution_group (
  tenant_id uuid not null, id uuid not null, code text not null, name text not null, policy text not null default 'Suggest', state text not null default 'Active',
  primary key (tenant_id,id), unique (tenant_id,code), check (policy in ('Suggest','AutoApply')), check (state in ('Active','Archived'))
);
create table if not exists business_manufacturing.material_substitute (
  tenant_id uuid not null, id uuid not null, group_id uuid not null, product_variant_id uuid not null, priority integer not null default 1,
  valid_from date not null, valid_to date, replacement_ratio numeric(28,10) not null default 1, uom_code text not null, auto_apply_allowed boolean not null default false,
  max_cost_increase_pct numeric(9,4), quality_approval_required boolean not null default false, primary key (tenant_id,id), unique (tenant_id,group_id,product_variant_id,valid_from),
  foreign key (tenant_id,group_id) references business_manufacturing.material_substitution_group(tenant_id,id), check (priority > 0), check (replacement_ratio > 0), check (valid_to is null or valid_to >= valid_from), check (max_cost_increase_pct is null or max_cost_increase_pct >= 0)
);

create table if not exists business_manufacturing.work_center (
  tenant_id uuid not null, id uuid not null, plant_id uuid not null, code text not null, name text not null, capacity_model text not null default 'Hours',
  time_zone text not null default 'UTC', default_efficiency numeric(9,6) not null default 1, state text not null default 'Active', row_version bigint not null default 1,
  primary key (tenant_id,id), unique (tenant_id,plant_id,code), foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id),
  check (capacity_model in ('Hours','Units','Cycles','Batches')), check (default_efficiency > 0 and default_efficiency <= 1), check (state in ('Active','Inactive')), check (row_version > 0)
);
create table if not exists business_manufacturing.work_center_calendar (
  tenant_id uuid not null, id uuid not null, work_center_id uuid not null, weekday smallint not null, shift_start time not null, shift_end time not null,
  effective_from date not null, effective_to date, break_minutes numeric(18,4) not null default 0, primary key (tenant_id,id),
  foreign key (tenant_id,work_center_id) references business_manufacturing.work_center(tenant_id,id), check (weekday between 0 and 6), check (shift_end > shift_start), check (break_minutes >= 0), check (effective_to is null or effective_to >= effective_from)
);
create table if not exists business_manufacturing.manufacturing_resource (
  tenant_id uuid not null, id uuid not null, plant_id uuid not null, resource_type text not null, employee_id uuid, fixed_asset_id uuid, name text not null,
  state text not null default 'Active', primary key (tenant_id,id), foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id),
  check (resource_type in ('Labor','Machine','Tool','External')), check (employee_id is not null or fixed_asset_id is not null or resource_type='External'), check (state in ('Active','Inactive'))
);

create table if not exists business_manufacturing.planning_run (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, plant_id uuid, horizon_start date not null, horizon_end date not null,
  as_of timestamptz not null, policy_version text not null default '1', state text not null default 'Draft', source_watermark jsonb not null default '{}'::jsonb,
  started_at timestamptz, completed_at timestamptz, error_code text, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id),
  foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id),
  check (horizon_end >= horizon_start), check (state in ('Draft','Running','Completed','Failed','Superseded','Cancelled'))
);
create table if not exists business_manufacturing.planning_demand (
  tenant_id uuid not null, id uuid not null, planning_run_id uuid not null, product_variant_id uuid not null, plant_id uuid, source_type text not null,
  source_ref text not null, need_date date not null, quantity numeric(28,10) not null, uom_code text not null, consumed_forecast_qty numeric(28,10) not null default 0,
  stock_allocated_qty numeric(28,10) not null default 0, uncovered_qty numeric(28,10) not null default 0,
  primary key (tenant_id,id), foreign key (tenant_id,planning_run_id) references business_manufacturing.planning_run(tenant_id,id), check (quantity > 0), check (consumed_forecast_qty >= 0 and consumed_forecast_qty <= quantity)
);
create table if not exists business_manufacturing.planned_supply (
  tenant_id uuid not null, id uuid not null, planning_run_id uuid not null, product_variant_id uuid not null, plant_id uuid, supply_type text not null,
  source_ref text, need_date date not null, suggested_start date, quantity numeric(28,10) not null, uom_code text not null, state text not null default 'Suggested',
  source_watermark jsonb not null default '{}'::jsonb, firmed_order_id uuid, primary key (tenant_id,id), foreign key (tenant_id,planning_run_id) references business_manufacturing.planning_run(tenant_id,id),
  check (supply_type in ('Production','Purchase','Transfer')), check (quantity > 0), check (state in ('Suggested','Firmed','Cancelled','Stale'))
);
create table if not exists business_manufacturing.planning_exception (
  tenant_id uuid not null, id uuid not null, planning_run_id uuid not null, product_variant_id uuid, exception_type text not null, severity text not null default 'Warning',
  need_date date, quantity numeric(28,10), message text not null, resolved_at timestamptz, primary key (tenant_id,id), foreign key (tenant_id,planning_run_id) references business_manufacturing.planning_run(tenant_id,id),
  check (severity in ('Info','Warning','Error'))
);
create table if not exists business_manufacturing.mrp_pegging_link (
  tenant_id uuid not null, id uuid not null, planning_run_id uuid not null, demand_id uuid, requirement_id uuid, planned_supply_id uuid, parent_link_id uuid,
  allocated_qty numeric(28,10) not null, primary key (tenant_id,id), foreign key (tenant_id,planning_run_id) references business_manufacturing.planning_run(tenant_id,id),
  foreign key (tenant_id,demand_id) references business_manufacturing.planning_demand(tenant_id,id), foreign key (tenant_id,planned_supply_id) references business_manufacturing.planned_supply(tenant_id,id),
  foreign key (tenant_id,parent_link_id) references business_manufacturing.mrp_pegging_link(tenant_id,id), check (allocated_qty > 0)
);

create table if not exists business_manufacturing.production_order (
  tenant_id uuid not null, id uuid not null, number text not null, legal_entity_id uuid not null, plant_id uuid not null, product_variant_id uuid not null,
  ordered_qty numeric(28,10) not null, uom_code text not null, need_date date not null, planned_start date, planned_end date,
  source_type text not null default 'Manual', source_ref text, state text not null default 'Draft', material_state text not null default 'NotPlanned',
  operation_state text not null default 'NotStarted', quality_state text not null default 'NotRequired', cost_state text not null default 'Uncosted',
  completed_qty numeric(28,10) not null default 0, scrap_qty numeric(28,10) not null default 0, row_version bigint not null default 1,
  created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(), released_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,number), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,plant_id) references business_manufacturing.plant(tenant_id,id),
  check (ordered_qty > 0), check (completed_qty >= 0 and completed_qty <= ordered_qty), check (scrap_qty >= 0), check (state in ('Draft','Released','InProgress','OnHold','Completed','Closed','Cancelled')),
  check (material_state in ('NotPlanned','Short','PartiallyReady','Ready','Issued','Resolved')), check (operation_state in ('NotRequired','NotStarted','InProgress','PartiallyComplete','Complete','Blocked')),
  check (quality_state in ('NotRequired','Pending','Passed','Blocked')), check (cost_state in ('Uncosted','Planned','Accumulating','ReadyToClose','Finalized','Corrected')), check (row_version > 0)
);
create table if not exists business_manufacturing.production_definition_snapshot (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, production_definition_id uuid not null, bom_version_id uuid, formula_version_id uuid,
  routing_version_id uuid, snapshot jsonb not null, source_hash text not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,production_order_id),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id), check (jsonb_typeof(snapshot)='object')
);
create table if not exists business_manufacturing.production_material_requirement (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, line_ref uuid, product_variant_id uuid not null, required_qty numeric(28,10) not null,
  uom_code text not null, need_by date not null, preferred_product_variant_id uuid not null, substitute_group_id uuid, reserved_qty numeric(28,10) not null default 0,
  issued_qty numeric(28,10) not null default 0, consumed_qty numeric(28,10) not null default 0, returned_qty numeric(28,10) not null default 0, scrapped_qty numeric(28,10) not null default 0,
  state text not null default 'Required', primary key (tenant_id,id), foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  check (required_qty > 0), check (reserved_qty >= 0 and issued_qty >= 0 and consumed_qty >= 0 and returned_qty >= 0 and scrapped_qty >= 0),
  check (consumed_qty + returned_qty + scrapped_qty <= issued_qty), check (state in ('Required','Short','Reserved','PartiallyIssued','Issued','PartiallyConsumed','Consumed','Resolved'))
);
create table if not exists business_manufacturing.production_material_event (
  tenant_id uuid not null, id uuid not null, requirement_id uuid not null, event_type text not null, quantity numeric(28,10) not null, uom_code text not null,
  product_variant_id uuid not null, inventory_operation_id uuid, inventory_reservation_id uuid, source_key text not null, substitute_product_variant_id uuid,
  unit_cost numeric(28,10), currency_code char(3), occurred_at timestamptz not null default now(), actor_id uuid not null, primary key (tenant_id,id),
  unique (tenant_id,source_key), foreign key (tenant_id,requirement_id) references business_manufacturing.production_material_requirement(tenant_id,id),
  check (event_type in ('Reserved','Issued','Consumed','Returned','Scrapped')), check (quantity > 0), check (unit_cost is null or unit_cost >= 0)
);
create table if not exists business_manufacturing.production_operation (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, sequence_no integer not null, name text not null, work_center_id uuid,
  setup_minutes numeric(18,4) not null default 0, run_minutes_per_unit numeric(18,6) not null default 0, planned_start timestamptz, planned_end timestamptz,
  state text not null default 'NotStarted', completed_qty numeric(28,10) not null default 0, actual_minutes numeric(18,4), quality_gate_required boolean not null default false,
  primary key (tenant_id,id), unique (tenant_id,production_order_id,sequence_no), foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  check (state in ('NotStarted','InProgress','Paused','PartiallyComplete','Complete','Blocked')), check (setup_minutes >= 0 and run_minutes_per_unit >= 0), check (completed_qty >= 0)
);
create table if not exists business_manufacturing.production_output (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, operation_id uuid, product_variant_id uuid not null, output_type text not null default 'Good',
  quantity numeric(28,10) not null, uom_code text not null, inventory_operation_id uuid, inventory_receipt_status text not null default 'Pending', lot_id uuid, serial_id uuid,
  provisional_value numeric(28,10), final_value numeric(28,10), currency_code char(3), idempotency_key text not null, reported_at timestamptz not null default now(),
  actor_id uuid not null, primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  foreign key (tenant_id,operation_id) references business_manufacturing.production_operation(tenant_id,id), check (output_type in ('Good','CoProduct','ByProduct')), check (quantity > 0), check (inventory_receipt_status in ('Pending','Committed','Unknown','Reversed'))
);
create table if not exists business_manufacturing.production_scrap (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, operation_id uuid, product_variant_id uuid not null, quantity numeric(28,10) not null,
  uom_code text not null, category text not null, reason text, inventory_operation_id uuid, idempotency_key text not null, actor_id uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id), check (quantity > 0),
  check (category in ('MaterialScrap','ProcessScrap','RejectedOutput','SetupScrap','QualityReject','NormalLoss'))
);
create table if not exists business_manufacturing.production_pause (
  tenant_id uuid not null, id uuid not null, production_operation_id uuid not null, reason text not null, started_at timestamptz not null, resumed_at timestamptz,
  notes text, actor_id uuid not null, primary key (tenant_id,id), foreign key (tenant_id,production_operation_id) references business_manufacturing.production_operation(tenant_id,id),
  check (reason in ('MaterialShortage','MachineBreakdown','QualityHold','OperatorUnavailable','WaitingInspection','PlannedBreak','Other'))
);

create table if not exists business_manufacturing.production_planned_cost_snapshot (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, currency_code char(3) not null, material_cost numeric(28,10) not null default 0,
  labor_cost numeric(28,10) not null default 0, machine_cost numeric(28,10) not null default 0, overhead_cost numeric(28,10) not null default 0, subcontract_cost numeric(28,10) not null default 0,
  total_cost numeric(28,10) not null default 0, source_hash text not null, captured_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,production_order_id),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id), check (material_cost >= 0 and labor_cost >= 0 and machine_cost >= 0 and overhead_cost >= 0 and subcontract_cost >= 0)
);
create table if not exists business_manufacturing.production_actual_cost_fact (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, source_type text not null, source_id text not null, cost_category text not null,
  amount numeric(28,10) not null, currency_code char(3) not null, quantity numeric(28,10), source_date date not null, correction_of_id uuid, idempotency_key text not null,
  created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id),
  foreign key (tenant_id,correction_of_id) references business_manufacturing.production_actual_cost_fact(tenant_id,id), check (amount >= 0), check (cost_category in ('Material','Labor','Machine','Overhead','Subcontract','Loss','Variance'))
);
create table if not exists business_manufacturing.production_variance (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, category text not null, planned_amount numeric(28,10) not null,
  actual_amount numeric(28,10) not null, variance_amount numeric(28,10) not null, currency_code char(3) not null, reason text, created_at timestamptz not null default now(), primary key (tenant_id,id),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id), check (category in ('MaterialUsage','MaterialValuation','LaborEfficiency','LaborRate','MachineEfficiency','Overhead','ScrapYield','Subcontract','Other'))
);
create table if not exists business_manufacturing.production_cost_projection (
  tenant_id uuid not null, id uuid not null, product_variant_id uuid not null, production_definition_id uuid not null, cost_basis text not null, as_of timestamptz not null,
  currency_code char(3) not null, material_cost numeric(28,10) not null default 0, labor_cost numeric(28,10) not null default 0, machine_cost numeric(28,10) not null default 0,
  overhead_cost numeric(28,10) not null default 0, subcontract_cost numeric(28,10) not null default 0, expected_loss_cost numeric(28,10) not null default 0, total_unit_cost numeric(28,10) not null default 0,
  source_version_hash text not null, primary key (tenant_id,id), unique (tenant_id,production_definition_id,cost_basis), check (cost_basis in ('InventoryValuation','StandardCost','ExpectedPurchase','RecentActual')), check (total_unit_cost >= 0)
);
create table if not exists business_manufacturing.component_usage_projection (
  tenant_id uuid not null, id uuid not null, component_product_variant_id uuid not null, production_definition_id uuid not null, bom_version_id uuid,
  required_qty numeric(28,10) not null, last_refreshed_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,component_product_variant_id,production_definition_id), check (required_qty > 0)
);
create table if not exists business_manufacturing.production_genealogy (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, output_id uuid not null, input_event_id uuid not null, input_lot_id uuid,
  output_lot_id uuid, input_quantity numeric(28,10) not null, output_quantity numeric(28,10) not null, certainty text not null default 'Exact', created_at timestamptz not null default now(), primary key (tenant_id,id),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id), foreign key (tenant_id,output_id) references business_manufacturing.production_output(tenant_id,id),
  foreign key (tenant_id,input_event_id) references business_manufacturing.production_material_event(tenant_id,id), check (certainty in ('Exact','Commingled','Possible')), check (input_quantity > 0 and output_quantity > 0)
);
create table if not exists business_manufacturing.production_external_reference (
  tenant_id uuid not null, id uuid not null, production_order_id uuid not null, source_module text not null, source_type text not null, source_id text not null,
  purpose text not null, snapshot jsonb not null default '{}'::jsonb, primary key (tenant_id,id), unique (tenant_id,production_order_id,source_module,source_type,source_id,purpose),
  foreign key (tenant_id,production_order_id) references business_manufacturing.production_order(tenant_id,id)
);
create table if not exists business_manufacturing.manufacturing_idempotency (
  tenant_id uuid not null, id uuid not null, operation text not null, idempotency_key text not null, request_hash text not null, response_json jsonb not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,operation,idempotency_key)
);
create table if not exists business_manufacturing.manufacturing_audit_event (
  tenant_id uuid not null, id uuid not null, entity_type text not null, entity_id uuid not null, action text not null, actor_id uuid,
  before_json jsonb, after_json jsonb, reason text, occurred_at timestamptz not null default now(), primary key (tenant_id,id)
);

create index if not exists ix_mfg_definition_resolve on business_manufacturing.production_definition(tenant_id,legal_entity_id,plant_id,product_variant_id,state,valid_from,priority);
create index if not exists ix_mfg_bom_component on business_manufacturing.bom_component_line(tenant_id,component_product_variant_id);
create index if not exists ix_mfg_orders on business_manufacturing.production_order(tenant_id,legal_entity_id,plant_id,state,need_date,updated_at desc);
create index if not exists ix_mfg_materials on business_manufacturing.production_material_requirement(tenant_id,production_order_id,state);
create index if not exists ix_mfg_plans on business_manufacturing.planned_supply(tenant_id,planning_run_id,state,need_date);
create index if not exists ix_mfg_work_center on business_manufacturing.production_operation(tenant_id,work_center_id,state,planned_start);
create index if not exists ix_mfg_genealogy_input on business_manufacturing.production_genealogy(tenant_id,input_lot_id,created_at desc);

do $$ declare t text; begin
  foreach t in array array['plant','manufacturing_product_profile','bom','bom_version','bom_component_line','formula','formula_version','formula_ingredient_line','formula_output_line','routing','routing_version','routing_operation','production_definition','material_substitution_group','material_substitute','work_center','work_center_calendar','manufacturing_resource','planning_run','planning_demand','planned_supply','planning_exception','mrp_pegging_link','production_order','production_definition_snapshot','production_material_requirement','production_material_event','production_operation','production_output','production_scrap','production_pause','production_planned_cost_snapshot','production_actual_cost_fact','production_variance','production_cost_projection','component_usage_projection','production_genealogy','production_external_reference','manufacturing_idempotency','manufacturing_audit_event'] loop
    execute format('alter table business_manufacturing.%I enable row level security', t);
    execute format('alter table business_manufacturing.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_manufacturing.%I', t);
    execute format('create policy tenant_isolation on business_manufacturing.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_manufacturing.%I to erp_app', t);
  end loop;
end $$;
