create schema if not exists business_quality authorization erp_owner;
revoke all on schema business_quality from public;
grant usage on schema business_quality to erp_app;

create table if not exists business_quality.quality_plan (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null,
 event_type text not null, mandatory boolean not null default true, status text not null default 'Active', row_version bigint not null default 1,
 created_by uuid not null, created_at timestamptz not null default now(), updated_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), check (event_type in ('Receiving','InProcess','Final','Outgoing','Return','AdHoc')), check (status in ('Active','Inactive','Archived')), check (row_version > 0)
);
create table if not exists business_quality.quality_plan_version (
 tenant_id uuid not null, id uuid not null, plan_id uuid not null, version_no integer not null, effective_from date not null, effective_to date,
 status text not null default 'Draft', notes text, created_by uuid not null, created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,plan_id,version_no), foreign key (tenant_id,plan_id) references business_quality.quality_plan(tenant_id,id),
 check (status in ('Draft','Approved','Active','Superseded','Archived')), check (effective_to is null or effective_to >= effective_from)
);
create table if not exists business_quality.quality_plan_applicability (
 tenant_id uuid not null, id uuid not null, plan_id uuid not null, product_variant_id uuid, supplier_party_id uuid, plant_id uuid, customer_party_id uuid, event_type text,
 primary key (tenant_id,id), unique (tenant_id,plan_id,product_variant_id,supplier_party_id,plant_id,customer_party_id,event_type), foreign key (tenant_id,plan_id) references business_quality.quality_plan(tenant_id,id)
);
create table if not exists business_quality.quality_trigger_rule (
 tenant_id uuid not null, id uuid not null, plan_id uuid not null, event_type text not null, priority integer not null default 100, condition_json jsonb not null default '{}'::jsonb,
 primary key (tenant_id,id), foreign key (tenant_id,plan_id) references business_quality.quality_plan(tenant_id,id), check (jsonb_typeof(condition_json)='object')
);
create table if not exists business_quality.quality_characteristic (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null, data_type text not null, base_uom_code text,
 required boolean not null default true, allowed_values jsonb, status text not null default 'Active', created_by uuid not null, created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), check (data_type in ('Numeric','Categorical','Text','Boolean')), check (allowed_values is null or jsonb_typeof(allowed_values)='array')
);
create table if not exists business_quality.quality_specification_version (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null, version_no integer not null default 1,
 effective_from date not null, effective_to date, status text not null default 'Active', created_by uuid not null, created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code,version_no), check (status in ('Draft','Active','Superseded','Archived')), check (effective_to is null or effective_to >= effective_from)
);
create table if not exists business_quality.quality_plan_characteristic (
 tenant_id uuid not null, id uuid not null, plan_version_id uuid not null, characteristic_id uuid not null, specification_version_id uuid,
 sequence_no integer not null default 1, minimum numeric(28,10), maximum numeric(28,10), target text, allowed_values jsonb, required boolean not null default true,
 primary key (tenant_id,id), unique (tenant_id,plan_version_id,characteristic_id), foreign key (tenant_id,plan_version_id) references business_quality.quality_plan_version(tenant_id,id), foreign key (tenant_id,characteristic_id) references business_quality.quality_characteristic(tenant_id,id),
 check (maximum is null or minimum is null or maximum >= minimum), check (allowed_values is null or jsonb_typeof(allowed_values)='array')
);
create table if not exists business_quality.inspection_method (
 tenant_id uuid not null, id uuid not null, code text not null, name text not null, method_type text not null default 'Manual', status text not null default 'Active', primary key (tenant_id,id), unique (tenant_id,code)
);
create table if not exists business_quality.sampling_plan (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, code text not null, name text not null, method text not null,
 fixed_quantity numeric(18,6), percentage numeric(7,4), status text not null default 'Active', created_by uuid not null, created_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,legal_entity_id,code), check (method in ('Full','Fixed','Percentage','Aql')), check (fixed_quantity is null or fixed_quantity > 0), check (percentage is null or percentage > 0 and percentage <= 100)
);
create table if not exists business_quality.sampling_plan_version (
 tenant_id uuid not null, id uuid not null, sampling_plan_id uuid not null, version_no integer not null, effective_from date not null, status text not null default 'Active',
 primary key (tenant_id,id), unique (tenant_id,sampling_plan_id,version_no), foreign key (tenant_id,sampling_plan_id) references business_quality.sampling_plan(tenant_id,id)
);
create table if not exists business_quality.quality_action_rule (
 tenant_id uuid not null, id uuid not null, plan_id uuid not null, failure_action text not null, priority integer not null default 100, primary key (tenant_id,id), foreign key (tenant_id,plan_id) references business_quality.quality_plan(tenant_id,id)
);

create table if not exists business_quality.inspection (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, number text not null, event_type text not null, state text not null default 'Draft', decision_state text not null default 'Pending',
 product_variant_id uuid, source_document_id uuid, production_order_id uuid, production_operation_id uuid, supplier_party_id uuid, customer_party_id uuid, plan_version_id uuid, sampling_plan_version_id uuid,
 lot_quantity numeric(28,10) not null, sample_size integer not null default 0, lot_reference text, source_fingerprint text, row_version bigint not null default 1, created_by uuid not null, created_at timestamptz not null default now(), completed_at timestamptz,
 primary key (tenant_id,id), unique (tenant_id,number), check (event_type in ('Receiving','InProcess','Final','Outgoing','Return','AdHoc')), check (state in ('Draft','InProgress','Completed','Cancelled','Superseded')), check (decision_state in ('Pending','Passed','Failed','Blocked','NotRequired')), check (lot_quantity > 0), check (sample_size >= 0), check (row_version > 0)
);
create table if not exists business_quality.inspection_plan_snapshot (
 tenant_id uuid not null, id uuid not null, inspection_id uuid not null, plan_id uuid not null, plan_version_id uuid not null, snapshot jsonb not null, captured_at timestamptz not null default now(),
 primary key (tenant_id,id), unique (tenant_id,inspection_id), foreign key (tenant_id,inspection_id) references business_quality.inspection(tenant_id,id), check (jsonb_typeof(snapshot)='object')
);
create table if not exists business_quality.quality_sample (
 tenant_id uuid not null, id uuid not null, inspection_id uuid not null, sample_no integer not null, quantity numeric(28,10) not null, status text not null default 'Open', primary key (tenant_id,id), unique (tenant_id,inspection_id,sample_no), foreign key (tenant_id,inspection_id) references business_quality.inspection(tenant_id,id), check (quantity > 0), check (status in ('Open','Tested','Rejected','Accepted'))
);
create table if not exists business_quality.inspection_result (
 tenant_id uuid not null, id uuid not null, inspection_id uuid not null, sample_id uuid, characteristic_id uuid not null, numeric_value numeric(28,10), text_value text, boolean_value boolean, uom_code text, passed boolean, evidence_reference text, corrected_from_id uuid, recorded_by uuid not null, recorded_at timestamptz not null default now(),
 primary key (tenant_id,id), foreign key (tenant_id,inspection_id) references business_quality.inspection(tenant_id,id), foreign key (tenant_id,sample_id) references business_quality.quality_sample(tenant_id,id), foreign key (tenant_id,characteristic_id) references business_quality.quality_characteristic(tenant_id,id), check ((numeric_value is not null)::integer + (text_value is not null)::integer + (boolean_value is not null)::integer = 1)
);
create table if not exists business_quality.inspection_result_correction (
 tenant_id uuid not null, id uuid not null, inspection_result_id uuid not null, reason text not null, corrected_by uuid not null, corrected_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,inspection_result_id) references business_quality.inspection_result(tenant_id,id)
);
create table if not exists business_quality.inspection_instrument_use (
 tenant_id uuid not null, id uuid not null, inspection_id uuid not null, instrument_ref text not null, calibration_status text not null, calibration_due_on date, calibration_snapshot jsonb not null default '{}'::jsonb, primary key (tenant_id,id), foreign key (tenant_id,inspection_id) references business_quality.inspection(tenant_id,id), check (calibration_status in ('Valid','Expired','Unknown','NotRequired'))
);
create table if not exists business_quality.inspection_review (
 tenant_id uuid not null, id uuid not null, inspection_id uuid not null, reviewer_id uuid not null, outcome text not null, notes text, reviewed_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,inspection_id) references business_quality.inspection(tenant_id,id), check (outcome in ('Accepted','Rejected','Returned'))
);
create table if not exists business_quality.inspection_evaluation (
 tenant_id uuid not null, id uuid not null, inspection_id uuid not null, decision text not null, evaluated_by uuid not null, evaluated_at timestamptz not null default now(), evidence_reference text not null, reason text,
 primary key (tenant_id,id), unique (tenant_id,inspection_id), foreign key (tenant_id,inspection_id) references business_quality.inspection(tenant_id,id), check (decision in ('Passed','Failed','Blocked'))
);

create table if not exists business_quality.quality_restriction (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, restriction_type text not null, status text not null default 'Active', reason_code text not null, product_variant_id uuid, warehouse_id uuid, location_id uuid, lot_id uuid, serial_id uuid, inspection_id uuid, quantity numeric(28,10), inventory_operation_id uuid, created_by uuid not null, created_at timestamptz not null default now(), released_by uuid, released_at timestamptz, release_reason text, primary key (tenant_id,id), check (status in ('Active','Released','Cancelled')), check (quantity is null or quantity > 0)
);
create table if not exists business_quality.quality_disposition (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, inspection_id uuid, decision text not null, state text not null default 'Draft', quantity numeric(28,10), reason text not null, external_reference text, created_by uuid not null, created_at timestamptz not null default now(), executed_by uuid, executed_at timestamptz, primary key (tenant_id,id), check (decision in ('Accept','Reject','UseAsIs','Concession','Return','Scrap','Rework','Partial')), check (state in ('Draft','Approved','Executed','Cancelled')), check (quantity is null or quantity > 0)
);
create table if not exists business_quality.quality_disposition_line (
 tenant_id uuid not null, id uuid not null, disposition_id uuid not null, product_variant_id uuid, lot_id uuid, serial_id uuid, quantity numeric(28,10) not null, decision text not null, primary key (tenant_id,id), foreign key (tenant_id,disposition_id) references business_quality.quality_disposition(tenant_id,id), check (quantity > 0)
);
create table if not exists business_quality.quality_deviation (
 tenant_id uuid not null, id uuid not null, inspection_id uuid, title text not null, reason text not null, state text not null default 'Open', approved_by uuid, approved_at timestamptz, primary key (tenant_id,id), check (state in ('Open','Approved','Rejected','Closed'))
);
create table if not exists business_quality.quality_concession (
 tenant_id uuid not null, id uuid not null, deviation_id uuid not null, customer_party_id uuid, approved_by uuid, approved_at timestamptz, expiry_date date, terms text not null, state text not null default 'Draft', primary key (tenant_id,id), foreign key (tenant_id,deviation_id) references business_quality.quality_deviation(tenant_id,id), check (state in ('Draft','Approved','Rejected','Expired'))
);

create table if not exists business_quality.quality_issue (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, issue_type text not null, title text not null, description text not null, severity text not null default 'Major', state text not null default 'Open', inspection_id uuid, supplier_party_id uuid, created_by uuid not null, created_at timestamptz not null default now(), closed_at timestamptz, primary key (tenant_id,id), check (severity in ('Minor','Major','Critical')), check (state in ('Open','Contained','Investigating','Resolved','Closed'))
);
create table if not exists business_quality.quality_defect (
 tenant_id uuid not null, id uuid not null, code text not null, name text not null, severity text not null default 'Major', active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,code), check (severity in ('Minor','Major','Critical'))
);
create table if not exists business_quality.nonconformance (
 tenant_id uuid not null, id uuid not null, number text not null, legal_entity_id uuid not null, issue_id uuid, title text not null, description text not null, severity text not null default 'Major', state text not null default 'Open', created_by uuid not null, created_at timestamptz not null default now(), closed_at timestamptz, primary key (tenant_id,id), unique (tenant_id,number), check (state in ('Open','Contained','CorrectiveAction','Verified','Closed','Cancelled'))
);
create table if not exists business_quality.nonconformance_defect (
 tenant_id uuid not null, id uuid not null, nonconformance_id uuid not null, defect_id uuid not null, quantity numeric(28,10), primary key (tenant_id,id), foreign key (tenant_id,nonconformance_id) references business_quality.nonconformance(tenant_id,id), foreign key (tenant_id,defect_id) references business_quality.quality_defect(tenant_id,id)
);
create table if not exists business_quality.quality_containment_action (
 tenant_id uuid not null, id uuid not null, nonconformance_id uuid, action_type text not null, description text not null, product_variant_id uuid, lot_id uuid, quantity numeric(28,10), state text not null default 'Open', created_by uuid not null, created_at timestamptz not null default now(), completed_at timestamptz, primary key (tenant_id,id), foreign key (tenant_id,nonconformance_id) references business_quality.nonconformance(tenant_id,id), check (state in ('Open','Completed','Cancelled'))
);
create table if not exists business_quality.nonconformance_relation (
 tenant_id uuid not null, id uuid not null, nonconformance_id uuid not null, related_type text not null, related_id uuid not null, primary key (tenant_id,id), unique (tenant_id,nonconformance_id,related_type,related_id), foreign key (tenant_id,nonconformance_id) references business_quality.nonconformance(tenant_id,id)
);

create table if not exists business_quality.capa (
 tenant_id uuid not null, id uuid not null, number text not null, legal_entity_id uuid not null, type text not null, title text not null, problem_statement text not null, state text not null default 'Open', ncr_id uuid, created_by uuid not null, created_at timestamptz not null default now(), closed_at timestamptz, primary key (tenant_id,id), unique (tenant_id,number), check (type in ('Corrective','Preventive','CorrectivePreventive')), check (state in ('Open','Investigating','ActionPending','EffectivenessPending','Closed','Cancelled'))
);
create table if not exists business_quality.capa_source_link (
 tenant_id uuid not null, id uuid not null, capa_id uuid not null, source_type text not null, source_id uuid not null, primary key (tenant_id,id), unique (tenant_id,capa_id,source_type,source_id), foreign key (tenant_id,capa_id) references business_quality.capa(tenant_id,id)
);
create table if not exists business_quality.capa_root_cause (
 tenant_id uuid not null, id uuid not null, capa_id uuid not null, category text not null, description text not null, verified boolean not null default false, primary key (tenant_id,id), foreign key (tenant_id,capa_id) references business_quality.capa(tenant_id,id)
);
create table if not exists business_quality.capa_action (
 tenant_id uuid not null, id uuid not null, capa_id uuid not null, action_type text not null, description text not null, owner_id uuid not null, due_date date not null, state text not null default 'Open', completed_at timestamptz, primary key (tenant_id,id), foreign key (tenant_id,capa_id) references business_quality.capa(tenant_id,id), check (state in ('Open','Completed','Cancelled'))
);
create table if not exists business_quality.capa_effectiveness_check (
 tenant_id uuid not null, id uuid not null, capa_id uuid not null, effective boolean not null, evidence_reference text not null, notes text not null, checked_by uuid not null, checked_at timestamptz not null default now(), primary key (tenant_id,id), foreign key (tenant_id,capa_id) references business_quality.capa(tenant_id,id)
);

create table if not exists business_quality.supplier_quality_profile (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, supplier_party_id uuid not null, risk_level text not null default 'Medium', target_ppm numeric(18,4) not null default 0, receiving_inspection_required boolean not null default false, status text not null default 'Active', row_version bigint not null default 1, primary key (tenant_id,id), unique (tenant_id,legal_entity_id,supplier_party_id), check (risk_level in ('Low','Medium','High','Critical')), check (target_ppm >= 0)
);
create table if not exists business_quality.supplier_corrective_action_request (
 tenant_id uuid not null, id uuid not null, number text not null, legal_entity_id uuid not null, supplier_party_id uuid not null, title text not null, description text not null, ncr_id uuid, due_date date, state text not null default 'Open', primary key (tenant_id,id), unique (tenant_id,number), check (state in ('Open','Submitted','Accepted','Rejected','Closed'))
);
create table if not exists business_quality.supplier_quality_review (
 tenant_id uuid not null, id uuid not null, profile_id uuid not null, review_date date not null, score numeric(7,2) not null, notes text, primary key (tenant_id,id), foreign key (tenant_id,profile_id) references business_quality.supplier_quality_profile(tenant_id,id), check (score between 0 and 100)
);

create table if not exists business_quality.quality_certificate_template (
 tenant_id uuid not null, id uuid not null, code text not null, name text not null, certificate_type text not null, template_json jsonb not null default '{}'::jsonb, active boolean not null default true, primary key (tenant_id,id), unique (tenant_id,code), check (certificate_type in ('COA','CoC'))
);
create table if not exists business_quality.quality_certificate (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, number text not null, certificate_type text not null, inspection_id uuid not null, customer_reference text, state text not null default 'Draft', created_by uuid not null, created_at timestamptz not null default now(), issued_at timestamptz, primary key (tenant_id,id), unique (tenant_id,number), check (certificate_type in ('COA','CoC')), check (state in ('Draft','Approved','Issued','Superseded','Cancelled'))
);
create table if not exists business_quality.quality_certificate_version (
 tenant_id uuid not null, id uuid not null, certificate_id uuid not null, version_no integer not null, snapshot jsonb not null, created_by uuid not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,certificate_id,version_no), foreign key (tenant_id,certificate_id) references business_quality.quality_certificate(tenant_id,id)
);
create table if not exists business_quality.quality_certificate_result_line (
 tenant_id uuid not null, id uuid not null, certificate_version_id uuid not null, characteristic_id uuid not null, result_text text not null, primary key (tenant_id,id), foreign key (tenant_id,certificate_version_id) references business_quality.quality_certificate_version(tenant_id,id)
);

create table if not exists business_quality.quality_cost_fact (
 tenant_id uuid not null, id uuid not null, legal_entity_id uuid not null, source_type text not null, source_id uuid not null, cost_category text not null, amount numeric(19,4) not null, currency_code char(3) not null, cost_date date not null, idempotency_key text not null, primary key (tenant_id,id), unique (tenant_id,idempotency_key), check (amount >= 0), check (length(currency_code)=3)
);
create table if not exists business_quality.quality_state_history (
 tenant_id uuid not null, id uuid not null, entity_type text not null, entity_id uuid not null, from_state text, to_state text not null, reason text, actor_id uuid, occurred_at timestamptz not null default now(), primary key (tenant_id,id)
);
create table if not exists business_quality.quality_audit_event (
 tenant_id uuid not null, id uuid not null, entity_type text not null, entity_id uuid not null, action text not null, actor_id uuid, before_json jsonb, after_json jsonb, occurred_at timestamptz not null default now(), primary key (tenant_id,id)
);
create table if not exists business_quality.quality_idempotency (
 tenant_id uuid not null, id uuid not null, operation text not null, idempotency_key text not null, request_hash text not null, response_json jsonb not null, created_at timestamptz not null default now(), primary key (tenant_id,id), unique (tenant_id,operation,idempotency_key)
);
create table if not exists business_quality.quality_integration_event (
 tenant_id uuid not null, message_id uuid not null, source_module text not null, message_type text not null, status text not null, payload jsonb not null, occurred_at timestamptz not null, processed_at timestamptz, primary key (tenant_id,message_id), check (status in ('Applied','Ignored','Failed'))
);

create index if not exists ix_quality_plans on business_quality.quality_plan(tenant_id,legal_entity_id,event_type,status,code);
create index if not exists ix_quality_applicability on business_quality.quality_plan_applicability(tenant_id,product_variant_id,supplier_party_id,event_type);
create index if not exists ix_quality_inspections on business_quality.inspection(tenant_id,legal_entity_id,event_type,decision_state,created_at desc,id desc);
create index if not exists ix_quality_source on business_quality.inspection(tenant_id,source_document_id);
create index if not exists ix_quality_restrictions on business_quality.quality_restriction(tenant_id,legal_entity_id,status,product_variant_id,lot_id);
create index if not exists ix_quality_issues on business_quality.quality_issue(tenant_id,legal_entity_id,state,severity,created_at desc);
create index if not exists ix_quality_ncr on business_quality.nonconformance(tenant_id,legal_entity_id,state,created_at desc);
create index if not exists ix_quality_capa on business_quality.capa(tenant_id,legal_entity_id,state,created_at desc);
create index if not exists ix_quality_history on business_quality.quality_state_history(tenant_id,entity_type,entity_id,occurred_at desc);

do $$ declare t text; begin
 foreach t in array array['quality_plan','quality_plan_version','quality_plan_applicability','quality_trigger_rule','quality_characteristic','quality_specification_version','quality_plan_characteristic','inspection_method','sampling_plan','sampling_plan_version','quality_action_rule','inspection','inspection_plan_snapshot','quality_sample','inspection_result','inspection_result_correction','inspection_instrument_use','inspection_review','inspection_evaluation','quality_restriction','quality_disposition','quality_disposition_line','quality_deviation','quality_concession','quality_issue','quality_defect','nonconformance','nonconformance_defect','quality_containment_action','nonconformance_relation','capa','capa_source_link','capa_root_cause','capa_action','capa_effectiveness_check','supplier_quality_profile','supplier_corrective_action_request','supplier_quality_review','quality_certificate_template','quality_certificate','quality_certificate_version','quality_certificate_result_line','quality_cost_fact','quality_state_history','quality_audit_event','quality_idempotency','quality_integration_event'] loop
   execute format('alter table business_quality.%I enable row level security',t);
   execute format('alter table business_quality.%I force row level security',t);
   execute format('drop policy if exists tenant_isolation on business_quality.%I',t);
   execute format('create policy tenant_isolation on business_quality.%I using (tenant_id = nullif(current_setting(''app.tenant_id'',true),'''')::uuid) with check (tenant_id = nullif(current_setting(''app.tenant_id'',true),'''')::uuid)',t);
   execute format('grant select,insert,update,delete on business_quality.%I to erp_app',t);
 end loop;
end $$;
