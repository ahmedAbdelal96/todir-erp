create schema if not exists business_projects authorization erp_owner;
revoke all on schema business_projects from public;
grant usage on schema business_projects to erp_app;

create table if not exists business_projects.project (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null,
  project_number text not null, name text not null, description text,
  project_type text not null, customer_party_id uuid, manager_employee_id uuid, manager_user_id uuid,
  start_date date not null, target_end_date date, actual_end_date date,
  lifecycle_state text not null default 'Draft', execution_health text not null default 'NotAssessed',
  billing_model text not null default 'NonBillable', currency_code char(3), source_type text, source_ref text,
  template_version_id uuid, row_version bigint not null default 1,
  created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,legal_entity_id,project_number),
  foreign key (tenant_id) references platform.tenant(id),
  foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id),
  check (project_type in ('CustomerProject','InternalProject','CapitalProject')),
  check (lifecycle_state in ('Draft','Active','OnHold','ExecutionCompleted','FinancialClosing','Closed','Cancelled')),
  check (execution_health in ('OnTrack','AtRisk','Delayed','Critical','NotAssessed')),
  check (billing_model in ('NonBillable','FixedPrice','Milestone','TimeAndMaterials','CostPlus','Mixed')),
  check ((project_type='CustomerProject') = (customer_party_id is not null)),
  check (project_type <> 'CapitalProject' or billing_model='NonBillable'),
  check (target_end_date is null or target_end_date >= start_date),
  check (actual_end_date is null or actual_end_date >= start_date),
  check (currency_code is null or length(currency_code)=3), check (row_version > 0)
);

create table if not exists business_projects.project_template (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid, code text not null, name text not null,
  project_type text not null, status text not null default 'Active', created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), check (status in ('Active','Archived')),
  check (project_type in ('CustomerProject','InternalProject','CapitalProject'))
);
create table if not exists business_projects.project_template_version (
  tenant_id uuid not null, id uuid not null, template_id uuid not null, version integer not null,
  effective_from date not null, effective_to date, definition jsonb not null default '{}'::jsonb,
  created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,template_id,version), foreign key (tenant_id,template_id) references business_projects.project_template(tenant_id,id),
  check (version > 0), check (effective_to is null or effective_to >= effective_from), check (jsonb_typeof(definition)='object')
);

create table if not exists business_projects.project_work_node (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, parent_node_id uuid,
  node_type text not null default 'Phase', name text not null, sequence_no integer not null, status text not null default 'Open',
  primary key (tenant_id,id), unique (tenant_id,project_id,sequence_no),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  foreign key (tenant_id,parent_node_id) references business_projects.project_work_node(tenant_id,id),
  check (node_type in ('Phase','WorkPackage','Wbs')), check (sequence_no > 0), check (status in ('Open','Completed','Cancelled'))
);
create table if not exists business_projects.project_task (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, work_node_id uuid, title text not null, description text,
  status text not null default 'Open', priority text not null default 'Normal', assigned_employee_id uuid,
  planned_start date, planned_end date, completed_at timestamptz, progress_percent numeric(5,2) not null default 0,
  sequence_no integer not null, row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,project_id,sequence_no),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  foreign key (tenant_id,work_node_id) references business_projects.project_work_node(tenant_id,id),
  check (status in ('Open','InProgress','Blocked','Completed','Cancelled')), check (priority in ('Low','Normal','High','Critical')),
  check (planned_end is null or planned_start is null or planned_end >= planned_start), check (progress_percent between 0 and 100), check (row_version > 0)
);
create table if not exists business_projects.project_dependency (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, predecessor_task_id uuid not null, successor_task_id uuid not null,
  dependency_type text not null default 'FinishToStart', lag_days integer not null default 0, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,predecessor_task_id,successor_task_id),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  foreign key (tenant_id,predecessor_task_id) references business_projects.project_task(tenant_id,id),
  foreign key (tenant_id,successor_task_id) references business_projects.project_task(tenant_id,id),
  check (predecessor_task_id <> successor_task_id), check (dependency_type in ('FinishToStart','StartToStart','FinishToFinish','StartToFinish'))
);
create table if not exists business_projects.project_milestone (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, task_id uuid, name text not null, due_date date,
  reached_at timestamptz, accepted_at timestamptz, acceptance_reference text, status text not null default 'Open',
  primary key (tenant_id,id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), foreign key (tenant_id,task_id) references business_projects.project_task(tenant_id,id),
  check (status in ('Open','Reached','Accepted','Cancelled')), check (accepted_at is null or reached_at is not null)
);
create table if not exists business_projects.project_deliverable (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, task_id uuid, milestone_id uuid, name text not null,
  description text, status text not null default 'Draft', submitted_at timestamptz, accepted_at timestamptz,
  acceptance_reference text, primary key (tenant_id,id),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  foreign key (tenant_id,task_id) references business_projects.project_task(tenant_id,id),
  foreign key (tenant_id,milestone_id) references business_projects.project_milestone(tenant_id,id),
  check (status in ('Draft','InProgress','Submitted','Accepted','Rejected','Cancelled')),
  check (status <> 'Accepted' or accepted_at is not null)
);

create table if not exists business_projects.project_team_member (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, employee_id uuid, user_id uuid, role_code text not null,
  allocation_percent numeric(7,2) not null default 100, active_from date not null, active_to date, is_manager boolean not null default false,
  primary key (tenant_id,id), unique (tenant_id,project_id,employee_id,role_code), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (employee_id is not null or user_id is not null), check (allocation_percent > 0 and allocation_percent <= 100), check (active_to is null or active_to >= active_from)
);
create table if not exists business_projects.project_resource_requirement (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, role_code text not null, quantity numeric(12,2) not null default 1,
  required_from date, required_to date, status text not null default 'Open', primary key (tenant_id,id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (quantity > 0), check (status in ('Open','PartiallyAllocated','Fulfilled','Cancelled')), check (required_to is null or required_from is null or required_to >= required_from)
);
create table if not exists business_projects.project_resource_allocation (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, requirement_id uuid, employee_id uuid, resource_ref text,
  allocated_from date not null, allocated_to date, allocation_percent numeric(7,2) not null, status text not null default 'Planned',
  primary key (tenant_id,id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), foreign key (tenant_id,requirement_id) references business_projects.project_resource_requirement(tenant_id,id),
  check (employee_id is not null or resource_ref is not null), check (allocation_percent > 0 and allocation_percent <= 100), check (status in ('Planned','Confirmed','Released')), check (allocated_to is null or allocated_to >= allocated_from)
);

create table if not exists business_projects.project_time_entry (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, task_id uuid, employee_id uuid not null, work_date date not null,
  duration_minutes integer not null, description text, billable boolean not null default false, state text not null default 'Draft',
  cost_rate numeric(19,4), cost_currency char(3), cost_amount numeric(19,4), billing_rate numeric(19,4), billing_currency char(3), billing_amount numeric(19,4),
  finalized_at timestamptz, correction_of_id uuid, idempotency_key text not null, row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), foreign key (tenant_id,task_id) references business_projects.project_task(tenant_id,id), foreign key (tenant_id,correction_of_id) references business_projects.project_time_entry(tenant_id,id),
  check (duration_minutes > 0 and duration_minutes <= 1440), check (state in ('Draft','Submitted','Approved','Rejected','Finalized','Corrected')), check (cost_amount is null or cost_amount >= 0), check (billing_amount is null or billing_amount >= 0), check (row_version > 0)
);
create table if not exists business_projects.project_time_approval (
  tenant_id uuid not null, id uuid not null, time_entry_id uuid not null, action text not null, actor_id uuid not null, reason text, occurred_at timestamptz not null default now(),
  primary key (tenant_id,id), foreign key (tenant_id,time_entry_id) references business_projects.project_time_entry(tenant_id,id), check (action in ('Submitted','Approved','Rejected','Finalized','Corrected'))
);

create table if not exists business_projects.project_rate_card (
  tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, project_id uuid, code text not null, name text not null,
  currency_code char(3) not null, effective_from date not null, effective_to date, status text not null default 'Active', primary key (tenant_id,id),
  unique (tenant_id,legal_entity_id,code,effective_from), foreign key (tenant_id,legal_entity_id) references platform.legal_entity(tenant_id,id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (status in ('Draft','Active','Retired')), check (effective_to is null or effective_to >= effective_from), check (length(currency_code)=3)
);
create table if not exists business_projects.project_rate_card_line (
  tenant_id uuid not null, id uuid not null, rate_card_id uuid not null, role_code text not null, employee_id uuid, cost_rate numeric(19,4), billing_rate numeric(19,4),
  primary key (tenant_id,id), unique (tenant_id,rate_card_id,role_code,employee_id), foreign key (tenant_id,rate_card_id) references business_projects.project_rate_card(tenant_id,id),
  check (cost_rate is null or cost_rate >= 0), check (billing_rate is null or billing_rate >= 0)
);

create table if not exists business_projects.project_budget (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, name text not null, currency_code char(3) not null, control_mode text not null default 'Warn',
  primary key (tenant_id,id), unique (tenant_id,project_id,name), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (control_mode in ('None','Warn','Approve','Block'))
);
create table if not exists business_projects.project_budget_version (
  tenant_id uuid not null, id uuid not null, budget_id uuid not null, version_no integer not null, state text not null default 'Draft', original_amount numeric(19,4) not null default 0, current_amount numeric(19,4) not null default 0,
  approved_by uuid, approved_at timestamptz, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,budget_id,version_no), foreign key (tenant_id,budget_id) references business_projects.project_budget(tenant_id,id),
  check (version_no > 0), check (state in ('Draft','Approved','Superseded')), check (original_amount >= 0 and current_amount >= 0), check (state <> 'Approved' or approved_at is not null)
);
create table if not exists business_projects.project_budget_line (
  tenant_id uuid not null, id uuid not null, budget_version_id uuid not null, category_code text not null, description text, amount numeric(19,4) not null,
  primary key (tenant_id,id), unique (tenant_id,budget_version_id,category_code), foreign key (tenant_id,budget_version_id) references business_projects.project_budget_version(tenant_id,id), check (amount >= 0)
);

create table if not exists business_projects.project_cost_fact (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, task_id uuid, source_module text not null, source_type text not null, source_id text not null, source_line_ref text,
  cost_category text not null, original_currency char(3) not null, original_amount numeric(19,4) not null, base_currency char(3) not null, base_amount numeric(19,4) not null, exchange_rate numeric(19,8),
  fact_date date not null, state text not null default 'Finalized', correction_of_id uuid, idempotency_key text not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), foreign key (tenant_id,task_id) references business_projects.project_task(tenant_id,id), foreign key (tenant_id,correction_of_id) references business_projects.project_cost_fact(tenant_id,id),
  check (original_amount >= 0 and base_amount >= 0), check (exchange_rate is null or exchange_rate > 0), check (state in ('Finalized','Reversed','Corrected')), check (length(original_currency)=3 and length(base_currency)=3)
);
create table if not exists business_projects.project_commitment (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, source_module text not null, source_id text not null, commitment_date date not null,
  currency_code char(3) not null, committed_amount numeric(19,4) not null, actualized_amount numeric(19,4) not null default 0, open_amount numeric(19,4) not null,
  state text not null default 'Open', idempotency_key text not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,idempotency_key), unique (tenant_id,source_module,source_id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (committed_amount >= 0 and actualized_amount >= 0 and open_amount >= 0 and actualized_amount <= committed_amount), check (state in ('Open','PartiallyActualized','Actualized','Cancelled'))
);
create table if not exists business_projects.project_forecast (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, version_no integer not null, state text not null default 'Draft',
  actual_amount numeric(19,4) not null default 0, open_commitment_amount numeric(19,4) not null default 0, etc_amount numeric(19,4) not null default 0, eac_amount numeric(19,4) not null default 0,
  includes_open_commitments boolean not null default true, assumptions text, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,project_id,version_no), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (state in ('Draft','Approved','Superseded')), check (actual_amount >= 0 and open_commitment_amount >= 0 and etc_amount >= 0 and eac_amount >= 0)
);

create table if not exists business_projects.project_billing_rule (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, version_no integer not null, model text not null, status text not null default 'Draft',
  contract_value numeric(19,4), currency_code char(3), markup_percent numeric(7,4) not null default 0, billing_cap numeric(19,4), effective_from date not null, effective_to date,
  primary key (tenant_id,id), unique (tenant_id,project_id,version_no), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (model in ('NonBillable','FixedPrice','Milestone','TimeAndMaterials','CostPlus','Mixed')), check (status in ('Draft','Active','Retired')), check (contract_value is null or contract_value >= 0), check (markup_percent >= 0), check (billing_cap is null or billing_cap >= 0), check (effective_to is null or effective_to >= effective_from)
);
create table if not exists business_projects.project_billing_plan_line (
  tenant_id uuid not null, id uuid not null, billing_rule_id uuid not null, milestone_id uuid, due_date date, amount numeric(19,4) not null, status text not null default 'Planned',
  primary key (tenant_id,id), foreign key (tenant_id,billing_rule_id) references business_projects.project_billing_rule(tenant_id,id), foreign key (tenant_id,milestone_id) references business_projects.project_milestone(tenant_id,id), check (amount >= 0), check (status in ('Planned','Eligible','Invoiced','WrittenOff'))
);
create table if not exists business_projects.project_billable_charge (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, source_type text not null, source_id text not null, charge_date date not null, description text not null,
  currency_code char(3) not null, cost_amount numeric(19,4) not null default 0, billable_amount numeric(19,4) not null, invoiced_amount numeric(19,4) not null default 0, state text not null default 'Eligible', invoice_request_id uuid,
  idempotency_key text not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,idempotency_key), unique (tenant_id,source_type,source_id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (cost_amount >= 0 and billable_amount >= 0 and invoiced_amount >= 0 and invoiced_amount <= billable_amount), check (state in ('Held','Eligible','Requested','PartiallyInvoiced','Invoiced','WrittenOff','Cancelled'))
);
create table if not exists business_projects.project_invoice_request (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, idempotency_key text not null, state text not null default 'Requested', requested_amount numeric(19,4) not null,
  currency_code char(3) not null, invoice_id uuid, failure_code text, requested_at timestamptz not null default now(), completed_at timestamptz, primary key (tenant_id,id), unique (tenant_id,idempotency_key), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (requested_amount >= 0), check (state in ('Requested','Accepted','Rejected','Unknown','Completed','Cancelled'))
);
create table if not exists business_projects.project_invoice_allocation (
  tenant_id uuid not null, id uuid not null, invoice_request_id uuid not null, charge_id uuid not null, invoice_id uuid, allocated_amount numeric(19,4) not null, allocated_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,invoice_request_id,charge_id), foreign key (tenant_id,invoice_request_id) references business_projects.project_invoice_request(tenant_id,id), foreign key (tenant_id,charge_id) references business_projects.project_billable_charge(tenant_id,id), check (allocated_amount >= 0)
);

create table if not exists business_projects.project_change_request (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, request_number text not null, title text not null, reason text, status text not null default 'Draft',
  contract_delta numeric(19,4) not null default 0, budget_delta numeric(19,4) not null default 0, schedule_delta_days integer not null default 0, requested_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,project_id,request_number), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (status in ('Draft','Submitted','Approved','Rejected','Cancelled'))
);
create table if not exists business_projects.project_change_order (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, change_request_id uuid not null, order_number text not null, status text not null default 'Approved',
  contract_delta numeric(19,4) not null, budget_delta numeric(19,4) not null, schedule_delta_days integer not null, approved_by uuid not null, approved_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,project_id,order_number), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), foreign key (tenant_id,change_request_id) references business_projects.project_change_request(tenant_id,id)
);
create table if not exists business_projects.project_baseline (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, baseline_type text not null, version_no integer not null, snapshot jsonb not null, created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,project_id,baseline_type,version_no), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (baseline_type in ('Scope','Budget','Schedule','Contract'))
);
create table if not exists business_projects.project_document (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, document_ref text not null, document_type text not null,
  sensitivity text not null default 'Normal', title text, created_by uuid not null, created_at timestamptz not null default now(),
  primary key (tenant_id,id), unique (tenant_id,project_id,document_ref),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (sensitivity in ('Normal','Confidential','Restricted'))
);
create table if not exists business_projects.project_external_reference (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, source_type text not null, source_id text not null,
  purpose text not null, snapshot jsonb, created_at timestamptz not null default now(), primary key (tenant_id,id),
  unique (tenant_id,project_id,source_type,source_id,purpose), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (snapshot is null or jsonb_typeof(snapshot)='object')
);

create table if not exists business_projects.project_close (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, state text not null default 'Open', requested_by uuid not null, requested_at timestamptz not null default now(), closed_by uuid, closed_at timestamptz, reopened_by uuid, reopened_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,project_id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), check (state in ('Open','Closing','Closed','Reopened'))
);
create table if not exists business_projects.project_close_exception (
  tenant_id uuid not null, id uuid not null, project_close_id uuid not null, exception_type text not null, reference_id text, message text not null, resolved_at timestamptz, primary key (tenant_id,id), foreign key (tenant_id,project_close_id) references business_projects.project_close(tenant_id,id)
);
create table if not exists business_projects.project_capital_handoff (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, source_cost_fact_id uuid not null, amount numeric(19,4) not null, currency_code char(3) not null, state text not null default 'Requested', external_reference text, idempotency_key text not null, requested_at timestamptz not null default now(), resolved_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,idempotency_key), unique (tenant_id,source_cost_fact_id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id), foreign key (tenant_id,source_cost_fact_id) references business_projects.project_cost_fact(tenant_id,id), check (amount > 0), check (state in ('Requested','Accepted','Rejected','Unknown'))
);
create table if not exists business_projects.project_audit_event (
  tenant_id uuid not null, id uuid not null, project_id uuid not null, action text not null, actor_id uuid, before_json jsonb, after_json jsonb, occurred_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id)
);
create table if not exists business_projects.project_idempotency (
  tenant_id uuid not null, id uuid not null, operation text not null, idempotency_key text not null, request_hash text not null, response_json jsonb not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,operation,idempotency_key)
);
create table if not exists business_projects.project_integration_event (
  tenant_id uuid not null, message_id uuid not null, source_module text not null, message_type text not null,
  project_id uuid, status text not null, reason text, payload jsonb not null, occurred_at timestamptz not null,
  processed_at timestamptz not null default now(), primary key (tenant_id,message_id),
  foreign key (tenant_id,project_id) references business_projects.project(tenant_id,id),
  check (status in ('Applied','Unallocated','Ignored','Failed')),
  check (jsonb_typeof(payload)='object')
);

create index if not exists ix_projects_register on business_projects.project(tenant_id,legal_entity_id,lifecycle_state,updated_at desc,id desc);
create index if not exists ix_projects_search on business_projects.project(tenant_id,legal_entity_id,name,project_number);
create index if not exists ix_projects_tasks on business_projects.project_task(tenant_id,project_id,status,sequence_no);
create index if not exists ix_projects_deliverables on business_projects.project_deliverable(tenant_id,project_id,status);
create index if not exists ix_projects_time on business_projects.project_time_entry(tenant_id,project_id,work_date desc,id desc);
create index if not exists ix_projects_cost on business_projects.project_cost_fact(tenant_id,project_id,fact_date desc,id desc);
create index if not exists ix_projects_commitment on business_projects.project_commitment(tenant_id,project_id,state,commitment_date desc);
create index if not exists ix_projects_charge on business_projects.project_billable_charge(tenant_id,project_id,state,charge_date desc);
create index if not exists ix_projects_audit on business_projects.project_audit_event(tenant_id,project_id,occurred_at desc);
create index if not exists ix_projects_integration_event on business_projects.project_integration_event(tenant_id,status,occurred_at desc);

do $$ declare t text; begin
  foreach t in array array['project','project_template','project_template_version','project_work_node','project_task','project_dependency','project_milestone','project_deliverable','project_team_member','project_resource_requirement','project_resource_allocation','project_time_entry','project_time_approval','project_rate_card','project_rate_card_line','project_budget','project_budget_version','project_budget_line','project_cost_fact','project_commitment','project_forecast','project_billing_rule','project_billing_plan_line','project_billable_charge','project_invoice_request','project_invoice_allocation','project_change_request','project_change_order','project_baseline','project_document','project_external_reference','project_close','project_close_exception','project_capital_handoff','project_audit_event','project_idempotency','project_integration_event'] loop
    execute format('alter table business_projects.%I enable row level security', t);
    execute format('alter table business_projects.%I force row level security', t);
    execute format('drop policy if exists tenant_isolation on business_projects.%I', t);
    execute format('create policy tenant_isolation on business_projects.%I using (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'', true), '''')::uuid)', t);
    execute format('grant select,insert,update,delete on business_projects.%I to erp_app', t);
  end loop;
end $$;
