-- Module 26 final acceptance corrections: immutable replay identity for
-- externally sourced labor and subcontract commitments.
alter table business_manufacturing.production_labor_fact
  add column if not exists request_hash text;

alter table business_manufacturing.production_subcontract_commitment
  add column if not exists request_hash text;

revoke all on table business_manufacturing.production_labor_fact from public;
revoke all on table business_manufacturing.production_subcontract_commitment from public;
grant select,insert,update,delete on table business_manufacturing.production_labor_fact to erp_app;
grant select,insert,update,delete on table business_manufacturing.production_subcontract_commitment to erp_app;
