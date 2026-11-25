-- Module 28 completion foundations. These tables hold planning-owned metadata and
-- rebuildable projections only; operational and statutory facts stay owner-owned.
create table if not exists business_planning.planning_dimension_member (
  tenant_id uuid not null, id uuid not null, dimension_id uuid not null, member_code text not null,
  display_name text not null, parent_code text, source_reference text, active boolean not null default true,
  created_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,dimension_id,member_code),
  foreign key (tenant_id,dimension_id) references business_planning.planning_dimension(tenant_id,id)
);
create table if not exists business_planning.planning_dimension_mapping (
  tenant_id uuid not null, id uuid not null, dimension_id uuid not null, member_id uuid not null,
  mapping_version text not null, source_module text not null, source_type text not null, source_key text not null,
  effective_from date, effective_to date, created_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,mapping_version,source_module,source_type,source_key),
  foreign key (tenant_id,dimension_id) references business_planning.planning_dimension(tenant_id,id),
  foreign key (tenant_id,member_id) references business_planning.planning_dimension_member(tenant_id,id),
  check (effective_to is null or effective_from is null or effective_from <= effective_to)
);
create table if not exists business_planning.planning_formula_dependency (
  tenant_id uuid not null, id uuid not null, formula_id uuid not null, dependency_code text not null,
  primary key (tenant_id,id), unique (tenant_id,formula_id,dependency_code),
  foreign key (tenant_id,formula_id) references business_planning.planning_formula(tenant_id,id)
);
create table if not exists business_planning.submission_unit (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null,
  department_ref uuid, name text not null, assigned_to uuid, state text not null default 'Draft',
  row_version bigint not null default 1, submitted_at timestamptz, returned_at timestamptz, approved_at timestamptz,
  created_by uuid not null, primary key (tenant_id,id),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id),
  check (state in ('Draft','Submitted','Returned','Approved','Locked'))
);
create table if not exists business_planning.submission_unit_coordinate (
  tenant_id uuid not null, id uuid not null, submission_unit_id uuid not null, coordinate_id uuid not null,
  primary key (tenant_id,id), unique (tenant_id,submission_unit_id,coordinate_id),
  foreign key (tenant_id,submission_unit_id) references business_planning.submission_unit(tenant_id,id),
  foreign key (tenant_id,coordinate_id) references business_planning.planning_coordinate(tenant_id,id)
);
create table if not exists business_planning.planning_spread_rule (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, source_coordinate_id uuid,
  method text not null, target_periods jsonb not null, weights jsonb, source_total numeric(19,4) not null,
  created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id),
  foreign key (tenant_id,source_coordinate_id) references business_planning.planning_coordinate(tenant_id,id),
  check (method in ('Even','Seasonal','PriorYear','Custom'))
);
create table if not exists business_planning.planning_allocation_rule (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, rule_code text not null,
  metric_code text not null, driver_type text not null, definition jsonb not null default '{}'::jsonb,
  version_no integer not null default 1, active boolean not null default true, created_by uuid not null,
  primary key (tenant_id,id), unique (tenant_id,plan_version_id,rule_code,version_no),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.planning_allocation_result (
  tenant_id uuid not null, id uuid not null, rule_id uuid not null, plan_version_id uuid not null,
  period_start date not null, legal_entity_id uuid not null, department_ref uuid, dimension_key jsonb not null default '{}'::jsonb,
  source_amount numeric(19,4) not null, weight numeric(19,8) not null, amount numeric(19,4) not null,
  created_at timestamptz not null default now(), primary key (tenant_id,id),
  foreign key (tenant_id,rule_id) references business_planning.planning_allocation_rule(tenant_id,id),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id),
  check (weight >= 0), check (amount >= 0)
);
create table if not exists business_planning.planning_statement_projection (
  tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, legal_entity_id uuid not null,
  statement_type text not null, line_code text not null, period_start date not null, amount numeric(19,4) not null,
  source_basis text not null, calculation_run_id uuid, created_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,plan_version_id,legal_entity_id,statement_type,line_code,period_start),
  foreign key (tenant_id,plan_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.forecast_accuracy_projection (
  tenant_id uuid not null, id uuid not null, forecast_version_id uuid not null, actual_version_id uuid not null,
  metric_code text not null, from_period date not null, to_period date not null, forecast_total numeric(19,4) not null,
  actual_total numeric(19,4) not null, absolute_error numeric(19,4) not null, percentage_error numeric(19,8) not null,
  bias numeric(19,4) not null, calculated_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,forecast_version_id,actual_version_id,metric_code,from_period,to_period),
  foreign key (tenant_id,forecast_version_id) references business_planning.plan_version(tenant_id,id),
  foreign key (tenant_id,actual_version_id) references business_planning.plan_version(tenant_id,id)
);
create table if not exists business_planning.scenario_access (
  tenant_id uuid not null, id uuid not null, scenario_id uuid not null, user_id uuid not null,
  access_level text not null default 'Read', created_by uuid not null, primary key (tenant_id,id),
  unique (tenant_id,scenario_id,user_id), foreign key (tenant_id,scenario_id) references business_planning.scenario(tenant_id,id),
  check (access_level in ('Read','Edit','Adopt'))
);

create index if not exists ix_planning_dimension_member on business_planning.planning_dimension_member(tenant_id,dimension_id,active);
create index if not exists ix_planning_submission_scope on business_planning.submission_unit(tenant_id,plan_version_id,state);
create index if not exists ix_planning_statement_scope on business_planning.planning_statement_projection(tenant_id,plan_version_id,legal_entity_id,period_start);
create index if not exists ix_planning_allocation_scope on business_planning.planning_allocation_result(tenant_id,plan_version_id,period_start);

do $$ declare t text; begin
  foreach t in array array['planning_dimension_member','planning_dimension_mapping','planning_formula_dependency','submission_unit','submission_unit_coordinate','planning_spread_rule','planning_allocation_rule','planning_allocation_result','planning_statement_projection','forecast_accuracy_projection','scenario_access'] loop
    execute format('alter table business_planning.%I enable row level security', t);
    execute format('alter table business_planning.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_planning.%I', t);
    execute format('create policy tenant_isolation on business_planning.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_planning.%I to erp_app', t);
  end loop;
end $$;
