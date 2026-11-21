-- Manufacturing execution attribution and external-cost integration references.
-- UUID references remain capability-level seams; Manufacturing does not own or
-- foreign-key Projects, HR, Quality, Service or Fixed Assets tables.

alter table business_manufacturing.production_order
  add column if not exists project_id uuid,
  add column if not exists project_task_id uuid;

create index if not exists ix_mfg_order_project
  on business_manufacturing.production_order(tenant_id,project_id,project_task_id)
  where project_id is not null;
