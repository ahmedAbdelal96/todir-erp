-- Module 24 corrective integration ledger. This is additive and safe on existing databases.
alter table if exists business_service.work_part_requirement
  add column if not exists legal_entity_id uuid,
  add column if not exists warehouse_id uuid,
  add column if not exists location_id uuid,
  add column if not exists unit_of_measure_id uuid,
  add column if not exists lot_id uuid,
  add column if not exists serial_id uuid;

alter table if exists business_service.service_charge
  add column if not exists invoice_integration_state text not null default 'NotRequested',
  add column if not exists invoice_request_key text,
  add column if not exists integration_error text,
  add column if not exists project_cost_fact_id uuid;
create unique index if not exists ux_service_charge_invoice_request_key
  on business_service.service_charge(tenant_id, invoice_request_key) where invoice_request_key is not null;

alter table if exists business_service.maintenance_due add column if not exists row_version bigint not null default 1;
alter table if exists business_service.work_order add column if not exists maintenance_due_id uuid;
create unique index if not exists ux_service_work_order_maintenance_due
  on business_service.work_order(tenant_id, maintenance_due_id) where maintenance_due_id is not null;

alter table if exists business_service.sla_profile
  add column if not exists working_weekdays smallint not null default 127,
  add column if not exists working_start time not null default time '08:00',
  add column if not exists working_end time not null default time '17:00',
  add column if not exists time_zone_id text not null default 'Africa/Cairo';

alter table if exists business_service.offline_command drop constraint if exists offline_command_state_check;
alter table if exists business_service.offline_command add constraint offline_command_state_check
  check (state in ('Pending','Accepted','Processed','Rejected','Retryable','Conflict'));

create table if not exists business_service.work_part_movement (
  tenant_id uuid not null, id uuid not null, part_requirement_id uuid not null,
  action text not null, quantity numeric(19,6) not null, state text not null default 'Pending',
  idempotency_key text not null, inventory_reservation_id uuid, inventory_operation_id uuid,
  inventory_reference text, error_code text, actor_id uuid not null,
  created_at timestamptz not null default now(), processed_at timestamptz,
  primary key (tenant_id,id), unique (tenant_id,idempotency_key),
  foreign key (tenant_id,part_requirement_id) references business_service.work_part_requirement(tenant_id,id),
  check (action in ('reserve','issue','consume','return','reverse')),
  check (state in ('Pending','Processed','Retryable','Conflict','Reversed')),
  check (quantity > 0)
);
create index if not exists ix_service_part_movement_part on business_service.work_part_movement(tenant_id,part_requirement_id,created_at);

create table if not exists business_service.service_contract_usage (
  tenant_id uuid not null, id uuid not null, contract_id uuid not null, work_order_id uuid not null,
  usage_type text not null, quantity numeric(19,6) not null, occurred_at timestamptz not null default now(),
  idempotency_key text not null, primary key (tenant_id,id), unique (tenant_id,idempotency_key),
  foreign key (tenant_id,contract_id) references business_service.service_contract(tenant_id,id),
  foreign key (tenant_id,work_order_id) references business_service.work_order(tenant_id,id), check (quantity > 0)
);

alter table business_service.work_part_movement enable row level security;
alter table business_service.work_part_movement force row level security;
drop policy if exists tenant_isolation on business_service.work_part_movement;
create policy tenant_isolation on business_service.work_part_movement
  using (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid)
  with check (tenant_id = nullif(current_setting('app.tenant_id', true), '')::uuid);
grant select,insert,update,delete on business_service.work_part_movement to erp_app;

create index if not exists ix_service_invoice_integration on business_service.service_charge(tenant_id,invoice_integration_state,state);
