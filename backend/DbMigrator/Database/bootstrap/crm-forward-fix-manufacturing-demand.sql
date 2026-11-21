-- CRM remains the commercial owner. A won opportunity interest is an explicit,
-- approved demand source; weighted/open opportunities are never firm MRP demand.
alter table business_crm.opportunity_interest
  add column if not exists uom_code text not null default 'EA';

alter table business_crm.opportunity_interest
  drop constraint if exists opportunity_interest_uom_code_check;

alter table business_crm.opportunity_interest
  add constraint opportunity_interest_uom_code_check check (length(trim(uom_code)) > 0);

create index if not exists ix_crm_manufacturing_demand
  on business_crm.opportunity(tenant_id, legal_entity_id, status, expected_close_date)
  where status = 'Won';

grant select, insert, update on business_crm.opportunity_interest to erp_app;
