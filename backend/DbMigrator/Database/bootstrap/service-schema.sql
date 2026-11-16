create schema if not exists business_service authorization erp_owner;
revoke all on schema business_service from public;
grant usage on schema business_service to erp_app;

create table if not exists business_service.service_work_type (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null,
  context text not null default 'Shared', default_duration_hours numeric(12,2), active boolean not null default true,
  primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code),
  foreign key (tenant_id) references platform.tenant(id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
  check (context in ('Shared','InternalMaintenance','CustomerFieldService')), check (default_duration_hours is null or default_duration_hours > 0)
);
create table if not exists business_service.service_work_template (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null,
  state text not null default 'Active', primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code),
  foreign key (tenant_id) references platform.tenant(id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
  check (state in ('Draft','Active','Archived'))
);
create table if not exists business_service.service_work_template_version (
  tenant_id uuid not null, id uuid not null, template_id uuid not null, version_no integer not null,
  effective_from date not null, effective_to date, expected_duration_hours numeric(12,2), definition jsonb not null default '{}'::jsonb,
  primary key (tenant_id,id), unique (tenant_id,template_id,version_no), foreign key (tenant_id,template_id) references business_service.service_work_template(tenant_id,id),
  check (version_no > 0), check (effective_to is null or effective_to >= effective_from), check (expected_duration_hours is null or expected_duration_hours > 0), check (jsonb_typeof(definition)='object')
);
create table if not exists business_service.installed_base_item (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, name text not null, serial_number text, serial_normalized text,
  model_number text, product_id uuid, customer_party_id uuid, owner_party_id uuid, parent_id uuid, site_name text, installed_date date,
  state text not null default 'Active', row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), foreign key (tenant_id) references platform.tenant(id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,parent_id) references business_service.installed_base_item(tenant_id,id),
  unique (tenant_id,serial_normalized), check (state in ('Draft','Active','Suspended','Retired')), check (row_version > 0), check (parent_id is null or parent_id <> id)
);
create table if not exists business_service.installed_base_component_event (
  tenant_id uuid not null, id uuid not null, item_id uuid not null, component_item_id uuid not null, event_type text not null, occurred_at timestamptz not null,
  replaced_component_id uuid, work_order_id uuid, notes text, primary key (tenant_id,id), foreign key (tenant_id,item_id) references business_service.installed_base_item(tenant_id,id),
  foreign key (tenant_id,component_item_id) references business_service.installed_base_item(tenant_id,id), check (event_type in ('Installed','Removed','Replaced'))
);
create table if not exists business_service.service_request (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, request_number text not null, context text not null, title text not null, description text,
  customer_party_id uuid, installed_base_id uuid, fixed_asset_id uuid, state text not null default 'New', priority text not null default 'Normal', requested_at timestamptz not null default now(),
  triaged_at timestamptz, resolved_at timestamptz, closed_at timestamptz, row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,request_number), foreign key (tenant_id) references platform.tenant(id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,installed_base_id) references business_service.installed_base_item(tenant_id,id),
  check (context in ('InternalMaintenance','CustomerFieldService')), check (state in ('New','Triaged','InProgress','Resolved','Closed','Cancelled')), check (priority in ('Low','Normal','High','Critical')), check (row_version > 0)
);
create table if not exists business_service.work_order (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, work_order_number text not null, context text not null, title text not null, description text,
  customer_party_id uuid, installed_base_id uuid, fixed_asset_id uuid, project_id uuid, service_request_id uuid, billing_owner text not null default 'None', work_type text not null default 'Corrective', priority text not null default 'Normal',
  state text not null default 'Draft', waiting_reason text, planned_start timestamptz, planned_end timestamptz, technical_completed_at timestamptz, accepted_at timestamptz, closed_at timestamptz, cancelled_at timestamptz,
  row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,work_order_number), foreign key (tenant_id) references platform.tenant(id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,installed_base_id) references business_service.installed_base_item(tenant_id,id), foreign key (tenant_id,service_request_id) references business_service.service_request(tenant_id,id),
  check (context in ('InternalMaintenance','CustomerFieldService')), check (billing_owner in ('Service','Projects','None')), check (state in ('Draft','Ready','Scheduled','InProgress','Waiting','TechnicalCompleted','Accepted','Closed','Cancelled')), check (priority in ('Low','Normal','High','Critical')), check (planned_end is null or planned_start is null or planned_end >= planned_start), check (row_version > 0),
  check (context = 'InternalMaintenance' or customer_party_id is not null), check (billing_owner <> 'Projects' or project_id is not null)
);
create table if not exists business_service.work_order_request_link (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, request_id uuid not null, linked_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,work_order_id,request_id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), foreign key (tenant_id,request_id) references business_service.service_request(tenant_id,id)
);
create table if not exists business_service.work_order_subject (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, subject_type text not null, subject_id uuid not null, primary key (tenant_id,id), unique (tenant_id,work_order_id,subject_type,subject_id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (subject_type in ('InstalledBase','FixedAsset'))
);
create table if not exists business_service.service_visit (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, planned_start timestamptz not null, planned_end timestamptz not null, location_type text not null default 'OnSite', site_instructions text,
  state text not null default 'Planned', actual_start timestamptz, actual_end timestamptz, row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (planned_end > planned_start), check (location_type in ('OnSite','Remote','Workshop','Depot')), check (state in ('Planned','Dispatched','Traveling','Arrived','InProgress','Paused','Completed','NoAccess','Cancelled')), check (row_version > 0)
);
create table if not exists business_service.work_assignment (
  tenant_id uuid not null, id uuid not null, visit_id uuid not null, employee_id uuid, user_id uuid, resource_type text not null default 'Technician', display_name text, state text not null default 'Assigned', assigned_at timestamptz not null default now(),
  primary key (tenant_id,id), foreign key (tenant_id,visit_id) references business_service.service_visit(tenant_id,id), check (employee_id is not null or user_id is not null or display_name is not null), check (state in ('Assigned','Accepted','Declined','Removed'))
);
create table if not exists business_service.service_resource_requirement (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, skill_code text, quantity numeric(12,2) not null default 1, required_from timestamptz, required_to timestamptz, state text not null default 'Open', primary key (tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (quantity > 0), check (state in ('Open','Fulfilled','Cancelled'))
);
create table if not exists business_service.work_checklist_answer (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, question_key text not null, question_type text not null, answer_json jsonb, required boolean not null default false, answered_at timestamptz, primary key (tenant_id,id), unique (tenant_id,work_order_id,question_key), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (question_type in ('Text','Boolean','Number','Choice','Measurement','Photo')), check (answer_json is null or jsonb_typeof(answer_json) in ('object','array','string','number','boolean'))
);
create table if not exists business_service.work_diagnosis (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, reported_symptom text, diagnosis text, failure_mode_code text, root_cause_code text, resolution_code text, notes text, recorded_by uuid not null, recorded_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id)
);
create table if not exists business_service.work_part_requirement (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, product_id uuid not null, quantity numeric(19,6) not null, uom_code text not null, description text, state text not null default 'Required', reserved_quantity numeric(19,6) not null default 0, issued_quantity numeric(19,6) not null default 0, consumed_quantity numeric(19,6) not null default 0, returned_quantity numeric(19,6) not null default 0, inventory_reference text, primary key (tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (quantity > 0), check (reserved_quantity >= 0 and issued_quantity >= 0 and consumed_quantity >= 0 and returned_quantity >= 0), check (consumed_quantity + returned_quantity <= issued_quantity), check (state in ('Required','Reserved','PartiallyReserved','Issued','Consumed','Returned','Unavailable','Ordered'))
);
create table if not exists business_service.service_labor_entry (
  tenant_id uuid not null, id uuid not null, visit_id uuid not null, employee_id uuid, started_at timestamptz not null, ended_at timestamptz not null, time_category text not null, cost_rate numeric(19,4), currency_code char(3) not null, cost_amount numeric(19,4), state text not null default 'Finalized', idempotency_key text not null, primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,visit_id) references business_service.service_visit(tenant_id,id), check (ended_at > started_at), check (time_category in ('Travel','OnSiteWork','RemoteWork','Waiting','Break','Administrative')), check (cost_rate is null or cost_rate >= 0), check (cost_amount is null or cost_amount >= 0)
);
create table if not exists business_service.service_cost_fact (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, source_module text not null, source_type text not null, source_id text not null, cost_category text not null, currency_code char(3) not null, amount numeric(19,4) not null, fact_date date not null, state text not null default 'Finalized', correction_of_id uuid, idempotency_key text not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), foreign key (tenant_id,correction_of_id) references business_service.service_cost_fact(tenant_id,id), check (amount >= 0), check (state in ('Finalized','Reversed','Corrected'))
);
create table if not exists business_service.warranty (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, warranty_type text not null, provider_party_id uuid, customer_party_id uuid, subject_id uuid, start_date date not null, end_date date not null, state text not null default 'Active', scope jsonb not null default '{}'::jsonb, primary key (tenant_id,id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), check (warranty_type in ('Customer','Supplier')), check (end_date >= start_date), check (state in ('Draft','Active','Expired','Cancelled'))
);
create table if not exists business_service.service_contract (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, contract_number text not null, contract_holder_party_id uuid not null, bill_to_party_id uuid not null, contract_type text not null, start_date date not null, end_date date not null, currency_code char(3) not null, value numeric(19,4), state text not null default 'Draft', scope jsonb not null default '{}'::jsonb, primary key (tenant_id,id), unique (tenant_id,contract_number), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), check (end_date >= start_date), check (value is null or value >= 0), check (state in ('Draft','Active','Suspended','Expired','Cancelled'))
);
create table if not exists business_service.service_contract_usage (
  tenant_id uuid not null, id uuid not null, contract_id uuid not null, work_order_id uuid not null, usage_type text not null, quantity numeric(19,6) not null, occurred_at timestamptz not null default now(), idempotency_key text not null, primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,contract_id) references business_service.service_contract(tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (quantity > 0)
);
create table if not exists business_service.coverage_decision (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, charge_category text not null, source_type text not null, source_id uuid, covered boolean not null, covered_amount numeric(19,4) not null default 0, customer_amount numeric(19,4) not null default 0, explanation text not null, decided_at timestamptz not null default now(), decided_by uuid not null, primary key (tenant_id,id), unique (tenant_id,work_order_id,charge_category), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (covered_amount >= 0 and customer_amount >= 0)
);
create table if not exists business_service.sla_profile (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, response_minutes integer not null, arrival_minutes integer, resolution_minutes integer not null, state text not null default 'Active', primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), check (response_minutes > 0 and resolution_minutes > 0), check (arrival_minutes is null or arrival_minutes > 0)
);
create table if not exists business_service.sla_clock (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, clock_type text not null, started_at timestamptz not null, due_at timestamptz not null, state text not null default 'Running', paused_minutes integer not null default 0, primary key (tenant_id,id), unique (tenant_id,work_order_id,clock_type), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (clock_type in ('Response','Arrival','Resolution')), check (state in ('Running','Paused','Completed','Breached'))
);
create table if not exists business_service.sla_clock_event (
  tenant_id uuid not null, id uuid not null, clock_id uuid not null, event_type text not null, occurred_at timestamptz not null default now(), reason text, actor_id uuid, primary key (tenant_id,id), foreign key (tenant_id,clock_id) references business_service.sla_clock(tenant_id,id)
);
create table if not exists business_service.service_charge (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, category text not null, description text not null, cost_amount numeric(19,4) not null default 0, covered_amount numeric(19,4) not null default 0, customer_amount numeric(19,4) not null default 0, currency_code char(3) not null, source_type text, source_id uuid, state text not null default 'Draft', invoice_id uuid, writeoff_reason text, primary key (tenant_id,id), unique (tenant_id,work_order_id,category,source_type,source_id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (cost_amount >= 0 and covered_amount >= 0 and customer_amount >= 0), check (state in ('Draft','Reviewed','Requested','Invoiced','WrittenOff','Cancelled'))
);
create table if not exists business_service.service_invoice_allocation (
  tenant_id uuid not null, id uuid not null, charge_id uuid not null, invoice_id uuid not null, allocated_amount numeric(19,4) not null, allocated_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,charge_id,invoice_id), foreign key (tenant_id,charge_id) references business_service.service_charge(tenant_id,id), check (allocated_amount >= 0)
);
create table if not exists business_service.service_report (
  tenant_id uuid not null, id uuid not null, work_order_id uuid not null, current_version integer not null default 1, state text not null default 'Draft', primary key (tenant_id,id), unique (tenant_id,work_order_id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id)
);
create table if not exists business_service.service_report_version (
  tenant_id uuid not null, id uuid not null, report_id uuid not null, version_no integer not null, content jsonb not null, signed_file_id uuid, accepted_by uuid, accepted_at timestamptz, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,report_id,version_no), foreign key (tenant_id,report_id) references business_service.service_report(tenant_id,id), check (jsonb_typeof(content)='object')
);
create table if not exists business_service.maintenance_plan (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, name text not null, subject_type text not null, subject_id uuid not null, trigger_type text not null, start_date date, interval_days integer, meter_threshold numeric(19,6), meter_code text, state text not null default 'Active', created_by uuid not null, primary key (tenant_id,id), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), check (subject_type in ('InstalledBase','FixedAsset')), check (trigger_type in ('CalendarFixed','FromLastCompletion','Meter')), check (interval_days is null or interval_days > 0), check (meter_threshold is null or meter_threshold > 0), check (trigger_type <> 'CalendarFixed' or start_date is not null), check (trigger_type <> 'Meter' or (meter_threshold is not null and meter_code is not null)), check (state in ('Draft','Active','Suspended','Retired'))
);
create table if not exists business_service.maintenance_due (
  tenant_id uuid not null, id uuid not null, plan_id uuid not null, subject_id uuid not null, due_date date not null, state text not null default 'Due', work_order_id uuid, reason text, generated_key text not null, primary key (tenant_id,id), unique (tenant_id,generated_key), foreign key (tenant_id,plan_id) references business_service.maintenance_plan(tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (state in ('Forecast','Due','Postponed','Skipped','Converted','Completed'))
);
create table if not exists business_service.meter_definition (
  tenant_id uuid not null, id uuid not null, subject_id uuid not null, meter_code text not null, uom text not null, data_type text not null default 'Decimal', rollover_value numeric(19,6), active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,subject_id,meter_code), check (data_type in ('Decimal','Integer')), check (rollover_value is null or rollover_value > 0)
);
create table if not exists business_service.meter_reading (
  tenant_id uuid not null, id uuid not null, subject_id uuid not null, meter_definition_id uuid not null, meter_code text not null, value numeric(19,6) not null, uom text not null, reading_at timestamptz not null, source text not null, is_reset boolean not null default false, correction_of_id uuid, primary key (tenant_id,id), unique (tenant_id,subject_id,meter_code,reading_at), foreign key (tenant_id,meter_definition_id) references business_service.meter_definition(tenant_id,id), foreign key (tenant_id,correction_of_id) references business_service.meter_reading(tenant_id,id), check (value >= 0)
);
create table if not exists business_service.asset_downtime (
  tenant_id uuid not null, id uuid not null, subject_id uuid not null, work_order_id uuid, downtime_type text not null, started_at timestamptz not null, ended_at timestamptz, reason text, primary key (tenant_id,id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (downtime_type in ('Planned','Unplanned')), check (ended_at is null or ended_at > started_at)
);
create table if not exists business_service.service_crew (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null, active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id)
);
create table if not exists business_service.service_skill (
  tenant_id uuid not null, id uuid not null, code text not null, name text not null, active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,code)
);
create table if not exists business_service.service_territory (
  tenant_id uuid not null, id uuid not null, code text not null, name text not null, active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,code)
);
create table if not exists business_service.offline_command (
  tenant_id uuid not null, id uuid not null, client_command_id uuid not null, command_type text not null, work_order_id uuid, visit_id uuid, expected_version bigint, payload jsonb not null default '{}'::jsonb, state text not null default 'Accepted', conflict_code text, result jsonb, received_at timestamptz not null default now(), processed_at timestamptz, primary key (tenant_id,id), unique (tenant_id,client_command_id), foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), foreign key (tenant_id,visit_id) references business_service.service_visit(tenant_id,id), check (state in ('Accepted','Processed','Rejected','Retryable','Conflict')), check (jsonb_typeof(payload)='object')
);
create table if not exists business_service.service_external_reference (
  tenant_id uuid not null, id uuid not null, entity_type text not null, entity_id uuid not null, source_module text not null, source_type text not null, source_id text not null, snapshot jsonb, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,source_module,source_type,source_id), check (snapshot is null or jsonb_typeof(snapshot)='object')
);
create table if not exists business_service.service_audit_event (
  tenant_id uuid not null, id uuid not null, entity_type text not null, entity_id uuid not null, action text not null, actor_id uuid, before_json jsonb, after_json jsonb, reason text, occurred_at timestamptz not null default now(), primary key (tenant_id,id)
);

create index if not exists ix_service_request_list on business_service.service_request(tenant_id,legal_entity_id,state,requested_at desc,id desc);
create index if not exists ix_service_request_subject on business_service.service_request(tenant_id,installed_base_id,state);
create index if not exists ix_service_work_list on business_service.work_order(tenant_id,legal_entity_id,state,planned_start,id desc);
create index if not exists ix_service_work_subject on business_service.work_order(tenant_id,installed_base_id,state);
create index if not exists ix_service_visit_list on business_service.service_visit(tenant_id,state,planned_start,id);
create index if not exists ix_service_visit_work on business_service.service_visit(tenant_id,work_order_id,planned_start);
create index if not exists ix_service_asset_search on business_service.installed_base_item(tenant_id,customer_party_id,state);
create index if not exists ix_service_asset_serial on business_service.installed_base_item(tenant_id,serial_normalized);
create index if not exists ix_service_due_list on business_service.maintenance_due(tenant_id,due_date,state,id);
create index if not exists ix_service_meter_list on business_service.meter_reading(tenant_id,subject_id,meter_code,reading_at desc);
create index if not exists ix_service_sla_deadlines on business_service.sla_clock(tenant_id,state,due_at);
create index if not exists ix_service_charge_list on business_service.service_charge(tenant_id,work_order_id,state,id);
create index if not exists ix_service_audit_list on business_service.service_audit_event(tenant_id,entity_type,entity_id,occurred_at desc);

do $$ declare t text; begin
  foreach t in array array['service_work_type','service_work_template','service_work_template_version','installed_base_item','installed_base_component_event','service_request','work_order','work_order_request_link','work_order_subject','service_visit','work_assignment','service_resource_requirement','work_checklist_answer','work_diagnosis','work_part_requirement','service_labor_entry','service_cost_fact','warranty','service_contract','service_contract_usage','coverage_decision','sla_profile','sla_clock','sla_clock_event','service_charge','service_invoice_allocation','service_report','service_report_version','maintenance_plan','maintenance_due','meter_definition','meter_reading','asset_downtime','service_crew','service_skill','service_territory','offline_command','service_external_reference','service_audit_event'] loop
    execute format('alter table business_service.%I enable row level security', t);
    execute format('alter table business_service.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_service.%I', t);
    execute format('create policy tenant_isolation on business_service.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_service.%I to erp_app', t);
  end loop;
end $$;
