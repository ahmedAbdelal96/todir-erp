alter table business_service.work_order
  add column if not exists template_id uuid,
  add column if not exists template_version integer;

alter table business_service.work_order
  drop constraint if exists work_order_template_version_fk;

-- PostgreSQL validates a new foreign key by reading the referencing table. The
-- runtime role is the forced-RLS owner, so suspend FORCE only for this DDL and
-- restore the tenant boundary before the migration commits.
alter table business_service.work_order no force row level security;
alter table business_service.service_work_template_version no force row level security;
alter table business_service.work_order
  add constraint work_order_template_version_fk
  foreign key (tenant_id, template_id, template_version)
  references business_service.service_work_template_version (tenant_id, template_id, version_no);
alter table business_service.work_order force row level security;
alter table business_service.service_work_template_version force row level security;

alter table business_service.work_order
  drop constraint if exists work_order_template_snapshot_check;

alter table business_service.work_order
  add constraint work_order_template_snapshot_check
  check ((template_id is null and template_version is null) or (template_id is not null and template_version is not null and template_version > 0));

create index if not exists ix_service_work_order_template_snapshot
  on business_service.work_order(tenant_id, template_id, template_version);
