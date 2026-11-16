alter table business_service.service_charge
  add column if not exists correction_of_id uuid;

alter table business_service.service_invoice_allocation
  add column if not exists invoice_line_id uuid;

alter table business_service.service_charge
  drop constraint if exists service_charge_correction_fk;
alter table business_service.service_charge no force row level security;
alter table business_service.service_charge
  add constraint service_charge_correction_fk
  foreign key (tenant_id, correction_of_id)
  references business_service.service_charge (tenant_id, id);
alter table business_service.service_charge force row level security;

create index if not exists ix_service_charge_correction
  on business_service.service_charge(tenant_id, correction_of_id)
  where correction_of_id is not null;

alter table business_service.service_report_version
  add column if not exists signed_file_id uuid;
