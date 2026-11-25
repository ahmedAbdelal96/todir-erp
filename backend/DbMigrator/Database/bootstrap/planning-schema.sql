-- Module 28: Budgeting, Financial Planning and FP&A.
-- The planning schema owns plans and projections only.  Operational and accounting
-- source facts remain owned by their modules and are represented by source metadata.
create schema if not exists business_planning;

create table if not exists business_planning.planning_cycle (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null,
  name text not null, cycle_type text not null, horizon text not null default 'Monthly',
  start_period date not null, end_period date not null, actual_cutoff date,
  state text not null default 'Draft', owner_user_id uuid, due_at timestamptz,
  row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), check (start_period <= end_period),
  check (cycle_type in ('AnnualBudget','RollingForecast','LongRangePlan','Target')), 
  check (horizon in ('Monthly','Quarterly','Annual')), check (state in ('Draft','Open','Collecting','Review','Approved','Published','Closed','Archived'))
);
create table if not exists business_planning.planning_model (
  tenant_id uuid not null, id uuid not null, name text not null, model_type text not null,
  legal_entity_id uuid, currency_code char(3) not null, status text not null default 'Draft',
  row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), check (model_type in ('Financial','Revenue','Opex','Workforce','Capex','Integrated')), check (status in ('Draft','Active','Archived'))
);
create table if not exists business_planning.planning_model_version (
  tenant_id uuid not null, id uuid not null, model_id uuid not null, version_no integer not null,
  definition jsonb not null default '{}'::jsonb, state text not null default 'Draft', created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,model_id,version_no), foreign key (tenant_id,model_id) references business_planning.planning_model(tenant_id,id), check (state in ('Draft','Published','Retired'))
);
create table if not exists business_planning.planning_dimension (
  tenant_id uuid not null, id uuid not null, model_version_id uuid not null, code text not null, name text not null,
  member_kind text not null, required boolean not null default false, active boolean not null default true,
  primary key (tenant_id,id), unique (tenant_id,model_version_id,code), foreign key (tenant_id,model_version_id) references business_planning.planning_model_version(tenant_id,id), check (member_kind in ('Account','LegalEntity','Department','Branch','Project','Product','Customer','EmployeeRole','Custom'))
);
create table if not exists business_planning.planning_metric (
  tenant_id uuid not null, id uuid not null, code text not null, name text not null, metric_kind text not null,
  data_type text not null default 'Money', source_policy text not null default 'Input', primary key (tenant_id,id), unique (tenant_id,code),
  check (metric_kind in ('Revenue','Cogs','Opex','Headcount','Capex','Cash','BalanceSheet','Kpi','Custom')), check (data_type in ('Money','Quantity','Percent','Days','Count')), check (source_policy in ('Input','Calculated','Actual','Source'))
);
create table if not exists business_planning.plan_version (
  tenant_id uuid not null, id uuid not null, cycle_id uuid not null, model_version_id uuid not null,
  name text not null, version_kind text not null, state text not null default 'Draft', scenario_id uuid,
  actual_cutoff date, baseline_version_id uuid, published_at timestamptz, row_version bigint not null default 1,
  created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), foreign key (tenant_id,cycle_id) references business_planning.planning_cycle(tenant_id,id), foreign key (tenant_id,model_version_id) references business_planning.planning_model_version(tenant_id,id),
  check (version_kind in ('Budget','Forecast','Target','Scenario')), check (state in ('Draft','Submitted','Approved','Published','Closed','Archived'))
);
create table if not exists business_planning.scenario (
  tenant_id uuid not null, id uuid not null, name text not null, scenario_type text not null default 'Base',
  base_version_id uuid, visibility text not null default 'Private', state text not null default 'Draft', assumptions jsonb not null default '{}'::jsonb,
  created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id),
  check (scenario_type in ('Base','Optimistic','Conservative','Stress','WhatIf')), check (visibility in ('Private','Shared')), check (state in ('Draft','Shared','Adopted','Archived'))
);
create table if not exists business_planning.planning_coordinate (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null,
  period_start date not null, metric_code text not null, dimension_key jsonb not null default '{}'::jsonb, signature text not null,
  primary key (tenant_id,id), unique (tenant_id,plan_version_id,legal_entity_id,period_start,metric_code,signature), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.planning_input_fact (
  tenant_id uuid not null, id uuid not null, coordinate_id uuid not null, amount numeric(19,4) not null,
  currency_code char(3) not null, value_kind text not null default 'Input', source_label text, explicit_zero boolean not null default false,
  source_watermark text, row_version bigint not null default 1, updated_by uuid not null, updated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,coordinate_id), foreign key (tenant_id,coordinate_id) references business_planning.planning_coordinate(tenant_id,id), check (value_kind in ('Input','Override'))
);
create table if not exists business_planning.planning_calculated_fact (
  tenant_id uuid not null, id uuid not null, coordinate_id uuid not null, amount numeric(19,4) not null, currency_code char(3) not null,
  calculation_run_id uuid not null, formula_code text not null, primary key (tenant_id,id), unique (tenant_id,coordinate_id), foreign key (tenant_id,coordinate_id) references business_planning.planning_coordinate(tenant_id,id)
);
create table if not exists business_planning.planning_actual_snapshot_fact (
  tenant_id uuid not null, id uuid not null, coordinate_id uuid not null, amount numeric(19,4) not null, currency_code char(3) not null,
  source_module text not null, source_reference text not null, source_watermark text not null, mapping_version text not null, refreshed_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,coordinate_id,source_module,source_reference), foreign key (tenant_id,coordinate_id) references business_planning.planning_coordinate(tenant_id,id)
);
create table if not exists business_planning.planning_source_fact (
  tenant_id uuid not null, id uuid not null, coordinate_id uuid not null, source_module text not null, source_type text not null, source_reference text not null,
  amount numeric(19,4) not null, currency_code char(3) not null, source_watermark text not null, primary key (tenant_id,id), unique (tenant_id,source_module,source_type,source_reference), foreign key (tenant_id,coordinate_id) references business_planning.planning_coordinate(tenant_id,id)
);
create table if not exists business_planning.planning_assumption (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, code text not null, name text not null, unit text not null,
  description text, primary key (tenant_id,id), unique (tenant_id,plan_version_id,code), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.planning_assumption_value (
  tenant_id uuid not null, id uuid not null, assumption_id uuid not null, period_start date not null, value numeric(19,6) not null,
  source text not null default 'Manual', primary key (tenant_id,id), unique (tenant_id,assumption_id,period_start), foreign key (tenant_id,assumption_id) references business_planning.planning_assumption(tenant_id,id)
);
create table if not exists business_planning.planning_formula (
  tenant_id uuid not null, id uuid not null, model_version_id uuid not null, code text not null, expression text not null,
  output_metric_code text not null, active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,model_version_id,code), foreign key (tenant_id,model_version_id) references business_planning.planning_model_version(tenant_id,id)
);
create table if not exists business_planning.planning_calculation_run (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, state text not null default 'Running', input_watermark text,
  started_at timestamptz not null default now(), completed_at timestamptz, error_detail text, primary key (tenant_id,id), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id), check (state in ('Running','Succeeded','Failed'))
);
create table if not exists business_planning.planning_calculation_trace (
  tenant_id uuid not null, id uuid not null, calculation_run_id uuid not null, coordinate_id uuid not null, formula_code text not null,
  inputs jsonb not null default '{}'::jsonb, output_amount numeric(19,4) not null, primary key (tenant_id,id), foreign key (tenant_id,calculation_run_id) references business_planning.planning_calculation_run(tenant_id,id)
);
create table if not exists business_planning.budget_baseline (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, name text not null, state text not null default 'Draft',
  original_total numeric(19,4) not null default 0, currency_code char(3) not null, approved_by uuid, approved_at timestamptz, primary key (tenant_id,id), unique (tenant_id,plan_version_id), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id), check (state in ('Draft','Approved','Published','Closed'))
);
create table if not exists business_planning.budget_revision (
  tenant_id uuid not null, id uuid not null, baseline_id uuid not null, delta_amount numeric(19,4) not null, reason text not null,
  state text not null default 'Draft', approved_by uuid, approved_at timestamptz, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,baseline_id) references business_planning.budget_baseline(tenant_id,id), check (state in ('Draft','Approved','Rejected'))
);
create table if not exists business_planning.budget_transfer (
  tenant_id uuid not null, id uuid not null, baseline_id uuid not null, from_coordinate_id uuid not null, to_coordinate_id uuid not null,
  from_bucket_id uuid, to_bucket_id uuid, amount numeric(19,4) not null, state text not null default 'Draft', reason text not null, approved_by uuid, approved_at timestamptz, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,baseline_id) references business_planning.budget_baseline(tenant_id,id), check (amount > 0), check (from_coordinate_id <> to_coordinate_id), check (state in ('Draft','Approved','Rejected'))
);
create table if not exists business_planning.budget_control_policy (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, name text not null, mode text not null default 'None', basis text not null default 'CommitmentAndActual', threshold_percent numeric(7,4) not null default 100, active boolean not null default true, primary key (tenant_id,id), check (mode in ('None','Warn','RequireApproval','Block')), check (basis in ('Precommitment','Commitment','CommitmentAndActual'))
);
create table if not exists business_planning.budget_control_bucket (
  tenant_id uuid not null, id uuid not null, policy_id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null,
  period_start date not null, metric_code text not null, dimension_key jsonb not null default '{}'::jsonb, original_budget numeric(19,4) not null default 0,
  revision_total numeric(19,4) not null default 0, transfer_in numeric(19,4) not null default 0, transfer_out numeric(19,4) not null default 0,
  actual_total numeric(19,4) not null default 0, commitment_total numeric(19,4) not null default 0, precommitment_total numeric(19,4) not null default 0, currency_code char(3) not null,
  row_version bigint not null default 1, primary key (tenant_id,id), unique (tenant_id,plan_version_id,legal_entity_id,period_start,metric_code,dimension_key), foreign key (tenant_id,policy_id) references business_planning.budget_control_policy(tenant_id,id), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.budget_reservation (
  tenant_id uuid not null, id uuid not null, bucket_id uuid not null, source_module text not null, source_document_id text not null, source_line_id text not null,
  source_version text not null, amount numeric(19,4) not null, state text not null default 'Reserved', created_at timestamptz not null default now(), released_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,source_module,source_document_id,source_line_id,source_version), foreign key (tenant_id,bucket_id) references business_planning.budget_control_bucket(tenant_id,id), check (amount > 0), check (state in ('Reserved','Consumed','Released','Corrected'))
);
create table if not exists business_planning.budget_consumption_fact (
  tenant_id uuid not null, id uuid not null, bucket_id uuid not null, source_module text not null, source_document_id text not null, source_line_id text not null,
  source_version text not null, amount numeric(19,4) not null, state text not null default 'Actual', correction_of_id uuid, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,source_module,source_document_id,source_line_id,source_version), foreign key (tenant_id,bucket_id) references business_planning.budget_control_bucket(tenant_id,id), check (amount > 0), check (state in ('Actual','Reversed'))
);
create table if not exists business_planning.budget_override (
  tenant_id uuid not null, id uuid not null, bucket_id uuid not null, amount numeric(19,4) not null, reason text not null, state text not null default 'Requested', requested_by uuid not null, approved_by uuid, created_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,bucket_id) references business_planning.budget_control_bucket(tenant_id,id), check (amount > 0), check (state in ('Requested','Approved','Rejected'))
);
create table if not exists business_planning.workforce_plan_line (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null, department_ref uuid, role_code text,
  period_start date not null, fte numeric(12,4) not null, average_cost numeric(19,4) not null, currency_code char(3) not null, source text not null default 'Manual', primary key (tenant_id,id), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id), check (fte >= 0), check (average_cost >= 0)
);
create table if not exists business_planning.capex_plan_item (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null, name text not null, category text not null,
  department_ref uuid, planned_amount numeric(19,4) not null, currency_code char(3) not null, purchase_period date not null, in_service_period date,
  priority integer not null default 3, state text not null default 'Draft', justification text, primary key (tenant_id,id), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id), check (planned_amount > 0), check (priority between 1 and 5), check (state in ('Draft','Prioritized','Approved','Rejected'))
);
create table if not exists business_planning.revenue_plan_driver (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null, period_start date not null,
  driver_key text not null, volume numeric(19,4) not null, price numeric(19,4) not null, currency_code char(3) not null, source text not null default 'Manual', primary key (tenant_id,id), unique (tenant_id,plan_version_id,legal_entity_id,period_start,driver_key), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.working_capital_assumption (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null, period_start date not null,
  dso numeric(12,4) not null default 0, dpo numeric(12,4) not null default 0, inventory_days numeric(12,4) not null default 0, primary key (tenant_id,id), unique (tenant_id,plan_version_id,legal_entity_id,period_start), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id), check (dso >= 0 and dpo >= 0 and inventory_days >= 0)
);
create table if not exists business_planning.planning_fx_rate (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, period_start date not null, from_currency char(3) not null, to_currency char(3) not null, rate numeric(19,8) not null, rate_type text not null default 'Planning', source text not null, primary key (tenant_id,id), unique (tenant_id,plan_version_id,period_start,from_currency,to_currency), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id), check (rate > 0), check (rate_type in ('Planning','Scenario'))
);
create table if not exists business_planning.actual_refresh_run (
  tenant_id uuid not null, id uuid not null, state text not null default 'Running', source_watermark text, mapping_version text not null, started_at timestamptz not null default now(), completed_at timestamptz, error_detail text, primary key (tenant_id,id), check (state in ('Running','Succeeded','Failed'))
);
create table if not exists business_planning.variance_projection (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null, period_start date not null, metric_code text not null,
  budget_amount numeric(19,4) not null default 0, forecast_amount numeric(19,4) not null default 0, actual_amount numeric(19,4) not null default 0, variance_amount numeric(19,4) not null default 0, materiality numeric(19,4) not null default 0, primary key (tenant_id,id), unique (tenant_id,plan_version_id,legal_entity_id,period_start,metric_code), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.planning_comment (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, coordinate_id uuid, body text not null, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.planning_audit_event (
  tenant_id uuid not null, id uuid not null, actor_user_id uuid not null, action text not null, entity_id uuid not null, occurred_at timestamptz not null default now(),
  primary key (tenant_id,id)
);

create index if not exists ix_planning_cycle_scope on business_planning.planning_cycle(tenant_id,legal_entity_id,state,start_period);
create index if not exists ix_planning_coordinate_grid on business_planning.planning_coordinate(tenant_id,plan_version_id,legal_entity_id,period_start,metric_code);
create index if not exists ix_planning_fact_coordinate on business_planning.planning_input_fact(tenant_id,coordinate_id);
create index if not exists ix_budget_bucket_scope on business_planning.budget_control_bucket(tenant_id,legal_entity_id,period_start,metric_code);
create index if not exists ix_budget_reservation_bucket on business_planning.budget_reservation(tenant_id,bucket_id,state);

do $$ declare t text; begin
  foreach t in array array['planning_cycle','planning_model','planning_model_version','planning_dimension','planning_metric','plan_version','scenario','planning_coordinate','planning_input_fact','planning_calculated_fact','planning_actual_snapshot_fact','planning_source_fact','planning_assumption','planning_assumption_value','planning_formula','planning_calculation_run','planning_calculation_trace','budget_baseline','budget_revision','budget_transfer','budget_control_policy','budget_control_bucket','budget_reservation','budget_consumption_fact','budget_override','workforce_plan_line','capex_plan_item','revenue_plan_driver','working_capital_assumption','planning_fx_rate','actual_refresh_run','variance_projection','planning_comment','planning_audit_event'] loop
    execute format('alter table business_planning.%I enable row level security', t);
    execute format('alter table business_planning.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_planning.%I', t);
    execute format('create policy tenant_isolation on business_planning.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_planning.%I to erp_app', t);
  end loop;
end $$;
grant usage on schema business_planning to erp_app;
